/* Copyright (c) 2021 Lyuma <xn.lyuma@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */
#undef DEBUG
#undef SIMPLE_DEBUG
// #define DEBUG
// #define SIMPLE_DEBUG
using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
//using VRC.SDK3.Avatars.Components;
using System.IO;
using System.Linq;

namespace tinycpp {
    using size_t = System.UInt64;
    using off_t = System.Int64;

    using int8_t = System.SByte;
    using uint8_t = System.Byte;
    using int16_t = System.Int16;
    using uint16_t = System.UInt16;
    using int32_t = System.Int32;
    using uint32_t = System.UInt32;
    using int64_t = System.Int64;
    using uint64_t = System.UInt64;

    // #if UNITY_EDITOR
    public class Preproc : Tokenizer
    {

        public interface OutputInterface {
            void EmitError(string msg);
            void EmitWarning(string msg);
            void Emit(string s, string filename, int line, int column);
            string IncludeFile(string fileContext, ref string filename);
        }

        public class macro {
            public int arg_count;
            public bool variadic;
            public bool objectlike;
            public string str_contents_buf = "";
            public List<string> argnames = new List<string>();
        }

        bool OBJECTLIKE(macro M) {
            return M.objectlike;
        }
        bool FUNCTIONLIKE(macro M) {
            return !M.objectlike;
        }
        int MACRO_ARGCOUNT(macro M) {
            return M.arg_count;
        }
        bool MACRO_VARIADIC(macro M) {
            return M.variadic;
        }

        public const int MAX_RECURSION = 32;

        int string_hash(string s) {
            UInt32 h = 0;
            int i = 0;
            while (i < s.Length) {
                h = 16*h + s[i++];
                h ^= h>>24 & 0xf0;
            }
            return (int)(h & 0xfffffff);
        }

            public List<string> includedirs;
            public Dictionary<string, macro> macros;
            public string last_file;
            public int last_line;
            public Tokenizer[] tchain = new Tokenizer[MAX_RECURSION];


        bool token_needs_string(Tokenizer.token tok) {
            switch(tok.type) {
                case TT_IDENTIFIER:
                case TT_WIDECHAR_LIT:
                case TT_WIDESTRING_LIT:
                case TT_SQSTRING_LIT:
                case TT_DQSTRING_LIT:
                        case TT_ELLIPSIS:
                        case TT_HEX_INT_LIT:
                        case TT_OCT_INT_LIT:
                        case TT_DEC_INT_LIT:
                case TT_FLOAT_LIT:
                case TT_UNKNOWN:
                    return true;
                default:
                    return false;
            }
        }

        static void tokenizer_from_file(Tokenizer t, string f) {
            t.tokenizer_init(f, (int)TF_PARSE_STRINGS);
            t.tokenizer_set_filename("<macro>");
            t.tokenizer_rewind();
        }
        static void tokenizer_from_file(Tokenizer t, StringBuilder f) {
            t.tokenizer_init(f.ToString(), (int)TF_PARSE_STRINGS);
            t.tokenizer_set_filename("<macro>");
            t.tokenizer_rewind();
        }

        macro get_macro(string name) {
            if (name == null) {
                return null;
            }
            if (!macros.ContainsKey(name)) {
                return null;
            }
            return macros[name];
        }

        void add_macro(string name, macro m) {
            macros[name] = m;
        }

        bool undef_macro(string name) {
            if (!macros.ContainsKey(name)) {
                return false;
            }
            macros.Remove(name);
            return true;
        }

        OutputInterface iface;

        public void set_output_interface(OutputInterface iface) {
            this.iface = iface;
        }

        void error_or_warning(string err, string type, Tokenizer t, token curr, bool error) {
            int column = curr.column != 0 ? curr.column : (t == null ? 0 : t.column);
            int line  = curr.line != 0 ? curr.line : (t == null ? 0 : t.line);
            StringBuilder ret = new StringBuilder(string.Format("<{0}> {1}:{2} {3}: '{4}': {5}\n", t == null ? "" : t.filename, line, column, type, err, curr.value));
            if (t != null) {
                ret .Append (t.buf + "\n");
                for(int i = 0; i < t.buf.Length; i++)
                    ret .Append ("^");
            }
            ret .Append("\n");
            if (error) {
                iface.EmitError(ret.ToString());
            } else {
                iface.EmitWarning(ret.ToString());
            }
        }
        void error(string err, Tokenizer t, token curr) {
            error_or_warning(err, "error", t, curr, true);
        }
        void warning(string err, Tokenizer t, token curr) {
            error_or_warning(err, "warning", t, curr, false);
        }

        void emit(string s, Tokenizer t, token curr) {
            iface.Emit(s, t.filename, curr.line, curr.column);
        }

        bool x_tokenizer_next_of(Tokenizer t, out Tokenizer.token tok, bool fail_unk) {
            bool ret = t.tokenizer_next(out tok);
            if(tok.type == TT_OVERFLOW) {
                error("max token length of 4095 exceeded!", t, tok);
                return false;
            } else if (fail_unk && ret == false) {
                error("Tokenizer encountered unknown token", t, tok);
                return false;
            }
            return true;
        }

        bool tokenizer_next(Tokenizer t, out Tokenizer.token tok) {
            return x_tokenizer_next_of(t, out tok, false);
        }
        bool x_tokenizer_next(Tokenizer t, out Tokenizer.token tok) {
            return x_tokenizer_next_of(t, out tok, true);
        }

        bool is_whitespace_token(Tokenizer.token token)
        {
            return token.type == TT_SEP &&
                (token.value == ' ' || token.value == '\t');
        }

        /* return index of matching item in values array, or -1 on error */
        int expect(Tokenizer t, int tt, string[] values, out Tokenizer.token token)
        {
            bool ret;
            bool goto_err = false;
            do {

                // Debug.Log("Peeking " + t.peeking + "/ " + t.buf + " /" + t.column + " " + (int)t.peek_token.type+ "," + t.buf);
                ret = t.tokenizer_next(out token);
                // Debug.Log("Peeking " + t.peeking + ":" + token + "/ " + t.buf + " /" + t.column + " " + ret + " " + (int)token.type);
                if(!ret || token.type == TT_EOF) {
                    goto_err = true;
                    break;
                }
            } while(is_whitespace_token(token));

            if(goto_err || token.type != tt) {
                error("unexpected token", t, token);
                return -1;
            }
            int i = 0;
            while(i < values.Length && values[i] != "") {
                // Debug.Log("Check expect " + i + ": " + (values[i]) + "==" + t.buf);
                if(values[i] == t.buf)
                    return i;
                ++i;
            }
            return -1;
        }

        bool is_char(Tokenizer.token tok, int ch) {
            return tok.type == TT_SEP && tok.value == ch;
        }

        string flush_whitespace(ref int ws_count) {
            if (ws_count <= 0) {
                return "";
            }
            string xout = "".PadLeft(ws_count, ' ');
            ws_count = 0;
            return xout;
        }

        /* skips until the next non-whitespace token (if the current one is one too)*/
        bool eat_whitespace(Tokenizer t, ref Tokenizer.token token, out int count) {
            count = 0;
            bool ret = true;
            while (is_whitespace_token(token)) {
                ++(count);
                ret = x_tokenizer_next(t, out token);
                if(!ret) break;
            }
            return ret;
        }
        /* fetches the next token until it is non-whitespace */
        bool skip_next_and_ws(Tokenizer t, out Tokenizer.token tok) {
            bool ret = t.tokenizer_next(out tok);
            if(!ret) return ret;
            int ws_count;
            ret = eat_whitespace(t, ref tok, out ws_count);
            return ret;
        }

        void emit_token(Tokenizer.token tok, Tokenizer t) {
            if(tok.type == TT_SEP) {
                emit("" + (char)tok.value, t, tok);
            } else if(t.buf != "" && token_needs_string(tok)) {
                emit(t.buf, t, tok);
            } else if (tok.type != TT_EOF) {
                error(string.Format("oops, dunno how to handle tt {0} ({1})\n", (int) tok.type, t.buf),null, tok);
            }
        }

        void emit_token_builder(StringBuilder builder, Tokenizer.token tok, Tokenizer t) {
            if(tok.type == TT_SEP) {
                builder.Append((char)tok.value);
            } else if(t.buf != "" && token_needs_string(tok)) {
                builder.Append(t.buf);
            } else if (tok.type != TT_EOF) {
                error(string.Format("oops, dunno how to handle tt {0} ({1})\n", (int) tok.type, t.buf),null, tok);
                return;
            }
            return;
        }

        string emit_token_str(Tokenizer.token tok, Tokenizer t) {
            if(tok.type == TT_SEP) {
                return "" + (char)tok.value;
            } else if(t.buf != "" && token_needs_string(tok)) {
                return t.buf;
            } else if (tok.type != TT_EOF) {
                error(string.Format("oops, dunno how to handle tt {0} ({1})\n", (int) tok.type, t.buf),null, tok);
                return "";
            }
            return "";
        }

        bool include_file(Tokenizer t) {
            string[] inc_chars = new string[]{ "\"", "<", ""};
            string[] inc_chars_end = new string[]{ "\"", ">", ""};
            Tokenizer.token tok;
            t.tokenizer_set_flags(0); // disable string tokenization

            int inc1sep = expect(t, TT_SEP, inc_chars, out tok);
            if(inc1sep == -1) {
                error("expected one of [\"<]", t, tok);
                return false;
            }
            bool ret = t.tokenizer_read_until(inc_chars_end[inc1sep], true);
            if(!ret) {
                error("error parsing filename", t, tok);
                return false;
            }
            // TODO: different path lookup depending on whether " or <
            string fn = t.buf;
            string filebuf = iface.IncludeFile(t.filename, ref fn);
            if (!(t.tokenizer_next(out tok) && is_char(tok, inc_chars_end[inc1sep][0]))) {
                Debug.LogError("assertion: Failed to get next token");
            }

            t.tokenizer_set_flags(TF_PARSE_STRINGS);
            return parse_file(fn, filebuf);
        }

        bool emit_error_or_warning(Tokenizer t, bool is_error) {
            int ws_count;
            bool ret = t.tokenizer_skip_chars(" \t", out ws_count);
            if(!ret) return ret;
            Tokenizer.token tmp = new Tokenizer.token();
            tmp.column = t.column;
            tmp.line = t.line;
            ret = t.tokenizer_read_until("\n", true);
            if(is_error) {
                error(t.buf, t, tmp);
                return false;
            }
            warning(t.buf, t, tmp);
            return true;
        }

        bool consume_nl_and_ws(Tokenizer t, out Tokenizer.token tok, int expected) {
            if(!x_tokenizer_next(t, out tok)) {
                error("unexpected", t, tok);
                return false;
            }
            if(expected != 0) {
                if(tok.type != TT_SEP || tok.value != expected) {
                    error("unexpected", t, tok);
                    return false;
                }
                switch(expected) {
                    case '\\' : expected = '\n'; break;
                    case '\n' : expected = 0; break;
                }
            } else {
                if(is_whitespace_token(tok)) {}
                else if(is_char(tok, '\\')) expected = '\n';
                else return true;
            }
            return consume_nl_and_ws(t, out tok, expected);
        }

        bool parse_macro(Tokenizer t) {
            int ws_count;
            bool ret = t.tokenizer_skip_chars(" \t", out ws_count);
            if(!ret) return ret;
            token curr; //tmp = {.column = t.column, .line = t.line};
            ret = t.tokenizer_next(out curr) && curr.type != TT_EOF;
            if(!ret) {
                error("parsing macro name", t, curr);
                return ret;
            }
            if(curr.type != TT_IDENTIFIER) {
                error("expected identifier", t, curr);
                return false;
            }
            string macroname = (t.buf);
        #if DEBUG
            Debug.Log("parsing macro " + macroname);
        #endif
            bool redefined = false;
            if(get_macro(macroname) != null) {
                if(macroname == "defined") {
                    error("\"defined\" cannot be used as a macro name", t, curr);
                    return false;
                }
                redefined = true;
            }

            macro mnew = new macro();
            mnew.objectlike = true;
            mnew.variadic = false;
            mnew.argnames = new List<string>();

            ret = x_tokenizer_next(t, out curr) && curr.type != TT_EOF;
            if(!ret) return ret;

            if (is_char(curr, '(')) {
                // Debug.Log(macroname + " open paren " + curr.value);
                mnew.objectlike = false;
                int expected = 0;
                while (true) {
                    /* process next function argument identifier */
                    ret = consume_nl_and_ws(t, out curr, expected);
                    if(!ret) {
                        error("unexpected", t, curr);
                        return ret;
                    }
                    expected = 0;
                    if(curr.type == TT_SEP) {
                        // Debug.Log(macroname + " Got cur value " + curr.value);
                        switch(curr.value) {
                        case '\\':
                            expected = '\n';
                            continue;
                        case ',':
                            continue;
                        case ')':
                            ret = t.tokenizer_skip_chars(" \t", out ws_count);
                            if(!ret) return ret;
                            goto break_loop1;
                        default:
                            error("unexpected character", t, curr);
                            return false;
                        }
                    } else if(!(curr.type == TT_IDENTIFIER || curr.type == TT_ELLIPSIS)) {
                        error("expected identifier for macro arg", t, curr);
                        return false;
                    }
                    {
                        if(curr.type == TT_ELLIPSIS) {
                            if(mnew.variadic) {
                                error("\"...\" isn't the last parameter", t, curr);
                                return false;
                            }
                            mnew.variadic = true;
                        }
                        string tmps = t.buf;
                        mnew.argnames.Add(tmps);
                    }
                    ++mnew.arg_count;
                }
                break_loop1:;
            } else if(is_whitespace_token(curr)) {
                ret = t.tokenizer_skip_chars(" \t", out ws_count);
                if(!ret) return ret;
            } else if(is_char(curr, '\n')) {
                /* content-less macro */
                goto done;
            }

            StringBuilder buf = new StringBuilder();

            bool backslash_seen = false;
            while(true) {
                /* ignore unknown tokens in macro body */
                ret = t.tokenizer_next(out curr);
                if(!ret) return false;
                if(curr.type == TT_EOF) break;
                if (curr.type == TT_SEP) {
                    if(curr.value == '\\')
                        backslash_seen = true;
                    else {
                        if(curr.value == '\n' && !backslash_seen) break;
                        emit_token_builder(buf, curr, t);
                        backslash_seen = false;
                    }
                } else {
                    emit_token_builder(buf, curr, t);
                }
            }
            if (mnew.objectlike) {
                Tokenizer tmp = new Tokenizer();
                string[] visited = new string[MAX_RECURSION];
                StringBuilder sb = new StringBuilder();
                sb.Append("AAAA_");
                sb.Append(macroname);
                sb.Append("()\n");
                tokenizer_from_file(tmp, sb);
                Tokenizer.token mname;
                tmp.tokenizer_next(out mname);
                macro mtmp = new macro();
                mtmp.arg_count = 0;
                mtmp.objectlike = false;
                mtmp.str_contents_buf = buf.ToString();
                add_macro("AAAA_" + macroname, mtmp);
                buf.Clear();
                expand_macro(tmp, ref buf, "AAAA_" + macroname, 1, visited);
                undef_macro("AAAA_" + macroname);
            }
            mnew.str_contents_buf = buf.ToString();
        done:
            if(redefined) {
                macro old = get_macro(macroname);
                string s_old = old.str_contents_buf;
                string s_new = mnew.str_contents_buf;
                if(s_old != s_new) {
                    warning("redefinition of macro " + macroname, t, new Tokenizer.token());
                }
            }
        // #if SIMPLE_DEBUG
            StringBuilder tmpstr = new StringBuilder();
            if (!mnew.objectlike) {
                tmpstr.Append('(');
                bool f = true;
                for (int xi = 0; xi < mnew.arg_count; xi++) {
                    if (!f) { tmpstr.Append(','); }
                    f = false;
                    tmpstr.Append(mnew.argnames[xi]);
                }
                tmpstr.Append(')');
            }
            Debug.Log("Defining " + macroname + tmpstr + "=" + mnew.str_contents_buf);
        // #endif
            add_macro(macroname, mnew);
            return true;
        }

        int macro_arglist_pos(macro m, string iden) {
            int i;
            for(i = 0; i < m.argnames.Count; i++) {
                string item = m.argnames[i];
                if(item == iden) return i;
            }
            return (int) -1;
        }


        class macro_info {
            public string name = "Unknown";
            public int nest;
            public int first;
            public int last;
        };

        bool was_visited(string name, string[] visited, int rec_level) {
            int x;
            for(x = rec_level; x >= 0; --x) {
                if(visited[x] == name) return true;
            }
            return false;
        }

        int get_macro_info(
            Tokenizer t,
            List<macro_info> mi_list,
            int nest, int tpos, string name,
            string[] visited, int rec_level) {
            int brace_lvl = 0;
            while (true) {
                Tokenizer.token tok;
                bool ret = t.tokenizer_next(out tok);
                if(!ret || tok.type == TT_EOF) break;
        // #if DEBUG
                Debug.Log(string.Format("({0}) nest {1}, brace {2} t: {3}", name, nest, brace_lvl, t.buf));
        // #endif
                macro m = new macro();
                string newname = (t.buf).ToString();
                if(tok.type == TT_IDENTIFIER && (m = get_macro(newname)) != null && !was_visited(newname, visited, rec_level)) {
                    if(FUNCTIONLIKE(m)) {
                        if(t.tokenizer_peek() == '(') {
                            int tpos_save = tpos;
                            tpos = get_macro_info(t, mi_list, nest+1, tpos+1, newname, visited, rec_level);
                            macro_info mi = new macro_info();
                            mi.name = newname;
                            mi.nest=nest+1;
                            mi.first = tpos_save;
                            mi.last = tpos + 1;
                            mi_list.Add(mi);
                        } else {
                            /* suppress expansion */
                        }
                    } else {
                        macro_info mi = new macro_info();
                        mi.name = newname;
                        mi.nest=nest+1;
                        mi.first = tpos;
                        mi.last = tpos + 1;
                        mi_list.Add(mi);
                    }
                } else if(is_char(tok, '(')) {
                    ++brace_lvl;
                } else if(is_char(tok, ')')) {
                    --brace_lvl;
                    if(brace_lvl == 0 && nest != 0) break;
                }
                ++tpos;
            }
            return tpos;
        }

        struct FILE_container {
            public StringBuilder buf;
            public Tokenizer t;
        };

        int mem_tokenizers_join(
            FILE_container org, FILE_container inj,
            out FILE_container result,
            int first, off_t lastpos) {
            result = new FILE_container();
            result.buf = new StringBuilder();
            int i;
            Tokenizer.token tok;
            bool ret;
            org.t.tokenizer_rewind();
            for(i=0; i<first; ++i) {
                ret = org.t.tokenizer_next(out tok);
                if (!ret) {//(!(ret && tok.type != TT_EOF)) {
                    Debug.LogError("Assert fail ret tok.type != eof");
                }
                emit_token_builder(result.buf, tok, org.t);
            }
            int cnt = 0, last = first;
            while(true) {
                ret = inj.t.tokenizer_next(out tok);
                if(!ret || tok.type == TT_EOF) break;
                emit_token_builder(result.buf, tok, inj.t);
                ++cnt;
            }
            while(org.t.tokenizer_ftello() < lastpos) {
                ret = org.t.tokenizer_next(out tok);
                last++;
            }

            int diff = cnt - ((int) last - (int) first);

            while (true) {
                ret = org.t.tokenizer_next(out tok);
                if(!ret || tok.type == TT_EOF) break;
                emit_token_builder(result.buf, tok, org.t);
            }

            result.t = new Tokenizer();
            tokenizer_from_file(result.t, result.buf);
            return diff;
        }

        int tchain_parens_follows(int rec_level) {
            int i, c = 0;
            for(i=rec_level;i>=0;--i) {
                c = tchain[i].tokenizer_peek();
                if(c == EOF) continue;
                if(c == '(') return i;
                else break;
            }
            return -1;
        }

        string stringify(Tokenizer t) {
            bool ret = true;
            Tokenizer.token tok;
            StringBuilder output = new StringBuilder();
            output.Append('\"');
            while (true) {
                ret = t.tokenizer_next(out tok);
                if(!ret) return "";
                if(tok.type == TT_EOF) break;
                if(is_char(tok, '\n')) continue;
                if(is_char(tok, '\\') && t.tokenizer_peek() == '\n') continue;
                if(tok.type == TT_DQSTRING_LIT) {
                    string s = t.buf;
                    int i = 0;
                    while(i < s.Length) {
                        if(s[i] == '\"') {
                            output.Append("\\\"");
                        } else if (s[i] == '\\') {
                            output.Append("\\\\");
                        } else {
                            output.Append(s[i]);
                        }
                        ++i;
                    }
                } else
                    emit_token_builder(output, tok, t);
            }
            output.Append('\"');
            return output.ToString();
        }

        /* rec_level -1 serves as a magic value to signal we're using
        expand_macro from the if-evaluator code, which means activating
        the "define" macro */
        bool expand_macro(Tokenizer t, ref StringBuilder buf, string name, int rec_level, string[] visited) {
            bool is_define = name == "defined";
            macro m;
            // if(is_define && rec_level != -1) {
            //     m = null;
            // } else {
            m = get_macro(name);
            // }
            if(m == null) {
                if (name.Contains("DEFAULT_UNITY")) {
                    Debug.Log("m is null for " + name);
                }
                buf.Append(name);
                return true;
            }
            if(rec_level == -1) rec_level = 0;
            if(rec_level >= MAX_RECURSION) {
                error("max recursion level reached", t, new Tokenizer.token());
                return false;
            }
        #if DEBUG
            // Debug.Log(string.Format("lvl {0}: expanding macro {1} ({2})", rec_level, name, m.str_contents_buf));
        #endif

            if(rec_level == 0 && t.filename == "<macro>") {
                last_file = t.filename;
                last_line = t.line;
            }
            if(name == "__FILE__") {
                buf.Append('\"');
                buf.Append(last_file);
                buf.Append('\"');
                return true;
            } else if(name == "__LINE__") {
                buf.Append("" + last_line);
                return true;
            }

            visited[rec_level] = name;
            tchain[rec_level] = t;

            int i;
            Tokenizer.token tok;
            int num_args = MACRO_ARGCOUNT(m);
            // Debug.Log("Macro count " + num_args + " buf " + buf + " name " + name);
            FILE_container[] argvalues = new FILE_container[(MACRO_VARIADIC(m) ? num_args + 1 : num_args)];

            for(i=0; i < num_args; i++) {
                argvalues[i] = new FILE_container();
                argvalues[i].buf = new StringBuilder();
            }
            int ws_count;
            /* replace named arguments in the contents of the macro call */
            if(FUNCTIONLIKE(m)) {
                int iret;
                while (char.IsWhiteSpace((char)t.tokenizer_peek())) {
                    t.tokenizer_getc();
                }
                if((iret = t.tokenizer_peek()) != '(') {
                    Debug.Log(name + ": Peeked a token " + t.filename + ":" + t.peek_token.line + " is " + (char)iret);
                    /* function-like macro shall not be expanded if not followed by '(' */
                    if(iret == EOF && rec_level > 0 && (iret = tchain_parens_follows(rec_level-1)) != -1) {
                        warning("Replacement text involved subsequent text", t, t.peek_token);
                        t = tchain[iret];
                    } else {
                        buf.Append(name);
                        Debug.Log("Go to cleanup: " + name);
                        return true;
                    }
                }
                bool xret = x_tokenizer_next(t, out tok);
                if (!(xret && is_char(tok, '('))) {
                    Debug.LogError("Invalid token " + tok);
                }

                int curr_arg = 0;
                bool need_arg = true;
                int parens = 0;
                
                if(!t.tokenizer_skip_chars(" \t", out ws_count)) return false;

                bool varargs = false;
                if(num_args == 1 && MACRO_VARIADIC(m)) varargs = true;
                while (true) {
                    xret = t.tokenizer_next(out tok);
                    if(!xret) return false;
                    if( tok.type == TT_EOF) {
                        warning("warning EOF\n", t, tok);
                        break;
                    }
                    if(parens == 0 && is_char(tok, ',') && !varargs) {
                        if(need_arg && 0 ==ws_count) {
                            /* empty argument is OK */
                        }
                        need_arg = true;
                        if(!varargs) curr_arg++;
                        if(curr_arg + 1 == num_args && MACRO_VARIADIC(m)) {
                            varargs = true;
                        } else if(curr_arg >= num_args) {
                            error("too many arguments for function macro", t, tok);
                            return false;
                        }
                        xret = t.tokenizer_skip_chars(" \t", out ws_count);
                        if(!xret) return xret;
                        continue;
                    } else if(is_char(tok, '(')) {
                        ++parens;
                    } else if(is_char(tok, ')')) {
                        if(0 == parens) {
                            if(0 != (curr_arg + num_args) && curr_arg < num_args-1) {
                                error("too few args for function macro", t, tok);
                                return false;
                            }
                            break;
                        }
                        --parens;
                    } else if(is_char(tok, '\\')) {
                        if(t.tokenizer_peek() == '\n') continue;
                    }
                    need_arg = false;
                    emit_token_builder(argvalues[curr_arg].buf, tok, t);
                }
            // } // LYUMA

            for(i=0; i < num_args; i++) {
                argvalues[i].t = new Tokenizer();
                tokenizer_from_file(argvalues[i].t, argvalues[i].buf);
        #if DEBUG
                Debug.Log(string.Format("macro argument {0}: {1}", (int) i, argvalues[i].buf));
        #endif
            }

            if(is_define) {
                if(get_macro(argvalues[0].buf.ToString()) != null)
                    buf.Append('1');
                else
                    buf.Append('0');
            }

            if(m.str_contents_buf.Length == 0) {
                Debug.Log("buf contents empty for " + name);
                return true;
            }

            FILE_container cwae = new FILE_container(); /* contents_with_args_expanded */
            cwae.buf = new StringBuilder();

            Tokenizer t2 = new Tokenizer();
            tokenizer_from_file(t2, m.str_contents_buf);
            int hash_count = 0;
            ws_count = 0;
            while (true) {
                bool ret;
                ret = t2.tokenizer_next(out tok);
                if(!ret) {
                    Debug.Log("Failed tokenizer " + name + ":" + m.str_contents_buf);
                    return false;
                }
                if(tok.type == TT_EOF) break;
                if(tok.type == TT_IDENTIFIER) {
                    cwae.buf.Append(flush_whitespace(ref ws_count));
                    string id = t2.buf.ToString();
                    if(MACRO_VARIADIC(m) && id == "__VA_ARGS__") {
                        id = "...";
                    }
                    int arg_nr = macro_arglist_pos(m, id);
                    if(arg_nr != (int) -1) {
                        argvalues[arg_nr].t.tokenizer_rewind();
                        if(hash_count == 1) {
                            cwae.buf.Append(stringify(argvalues[arg_nr].t));
                            ret = (cwae.buf.Length > 0);
                        }
                        else while (true) {
                            ret = argvalues[arg_nr].t.tokenizer_next(out tok);
                            if(!ret) return ret;
                            if(tok.type == TT_EOF) break;
                            emit_token_builder(cwae.buf, tok, argvalues[arg_nr].t);
                        }
                        hash_count = 0;
                    } else {
                        if(hash_count == 1) {
                            error("'#' is not followed by macro parameter", t2, tok);
                            return false;
                        }
                        emit_token_builder(cwae.buf, tok, t2);
                    }
                } else if(is_char(tok, '#')) {
                    if(hash_count != 0) {
                        error("'#' is not followed by macro parameter", t2, tok);
                        return false;
                    }
                    while (true) {
                        ++hash_count;
                        /* in a real cpp we'd need to look for '\\' first */
                        while(t2.tokenizer_peek() == '\n') {
                            x_tokenizer_next(t2, out tok);
                        }
                        if(t2.tokenizer_peek() == '#') x_tokenizer_next(t2, out tok);
                        else break;
                    }
                    if(hash_count == 1) cwae.buf.Append(flush_whitespace(ref ws_count));
                    else if(hash_count > 2) {
                        error("only two '#' characters allowed for macro expansion", t2, tok);
                        return false;
                    }
                    if(hash_count == 2)
                        ret = t2.tokenizer_skip_chars(" \t\n", out ws_count);
                    else
                        ret = t2.tokenizer_skip_chars(" \t", out ws_count);

                    if(!ret) {
                        Debug.Log("End of line?" + name);
                        return ret;
                    }
                    ws_count = 0;

                } else if(is_whitespace_token(tok)) {
                    ws_count++;
                } else {
                    if(hash_count == 1) {
                        error("'#' is not followed by macro parameter", t2, tok);
                        return false;
                    }
                    cwae.buf.Append(flush_whitespace(ref ws_count));
                    emit_token_builder(cwae.buf, tok, t2);
                }
            }
            cwae.buf.Append(flush_whitespace(ref ws_count));

            /* we need to expand macros after the macro arguments have been inserted */
            // if(true) { // LYUMA
        #if DEBUG
                Debug.Log("contents with args expanded: " + cwae.buf);
        #endif
                cwae.t = new Tokenizer();
                tokenizer_from_file(cwae.t, cwae.buf);
                Debug.Log("Expanding " + cwae.buf);
                // int mac_cnt = 0;
                // while (true) {
                //     bool ret = cwae.t.tokenizer_next(out tok);
                //     if(!ret) {
                //         Debug.Log("Failed null ret: " + name +" in " + cwae.buf);
                //         return ret;
                //     }
                //     if(tok.type == TT_EOF) break;
                //     if(tok.type == TT_IDENTIFIER && get_macro(cwae.t.buf) != null)
                //         ++mac_cnt;
                // }

                cwae.t.tokenizer_rewind();
                List<macro_info> mcs = new List<macro_info>();
                {
                    get_macro_info(cwae.t, mcs, 0, 0, "null", visited, rec_level);
                    /* some of the macros might not expand at this stage (without braces)*/
                }
                int depth = 0;
                int mac_cnt = mcs.Count;
                for(i = 0; i < mac_cnt; ++i) {
                    if(mcs[i].nest > depth) depth = mcs[i].nest;
                }
                while(depth > -1) {
                    Debug.Log("Looping " + name + ": depth=" + depth + " mac_cnt=" + mac_cnt);
                    for(i = 0; i < mac_cnt; ++i) if(mcs[i].nest == depth) {
                        macro_info mi = mcs[i];
                        cwae.t.tokenizer_rewind();
                        int j;
                        token utok;
                        for(j = 0; j < mi.first+1; ++j)
                            cwae.t.tokenizer_next(out utok);
                        FILE_container ct2 = new FILE_container();
                        FILE_container tmp = new FILE_container();
                        ct2.buf = new StringBuilder();
                        // cwae.t = new Tokenizer();
                        if(!expand_macro(cwae.t, ref ct2.buf, mi.name, rec_level+1, visited))
                            return false;
                        ct2.t = new Tokenizer();
                        tokenizer_from_file(ct2.t, ct2.buf);
                        /* manipulating the stream in case more stuff has been consumed */
                        off_t cwae_pos = cwae.t.tokenizer_ftello();
                        cwae.t.tokenizer_rewind();
        // #if DEBUG
                        Debug.Log("merging " + cwae.buf + " with " + ct2.buf);
        // #endif
                        int diff = mem_tokenizers_join(cwae, ct2, out tmp, mi.first, cwae_pos);
                        cwae = tmp;
        // #if DEBUG
                        Debug.Log("result: " + cwae.buf);
        // #endif
                        if(diff == 0) continue;
                        for(j = 0; j < mac_cnt; ++j) {
                            if(j == i) continue;
                            macro_info mi2 = mcs[j];
                            /* modified element mi can be either inside, after or before
                            another macro. the after case doesn't affect us. */
                            if(mi.first >= mi2.first && mi.last <= mi2.last) {
                                /* inside m2 */
                                mi2.last += diff;
                            } else if (mi.first < mi2.first) {
                                /* before m2 */
                                mi2.first += diff;
                                mi2.last += diff;
                            }
                        }
                    }
                    --depth;
                }
                cwae.t.tokenizer_rewind();
                while (true) {
                    macro ma;
                    cwae.t.tokenizer_next(out tok);
        // #if SIMPLE_DEBUG
                    Debug.Log("Expanding ...:" + cwae.t.buf + " into " + buf);
        // #endif
                    if(tok.type == TT_EOF) break;
                    if(tok.type == TT_IDENTIFIER && cwae.t.tokenizer_peek() == EOF &&
                    (ma = get_macro(cwae.t.buf.ToString())) != null && FUNCTIONLIKE(ma) && tchain_parens_follows(rec_level) != -1
                    ) {
                        bool ret = expand_macro(cwae.t, ref buf, cwae.t.buf.ToString(), rec_level+1, visited);
                        Debug.Log("Failure:" + cwae.t.buf + " into " + buf);
                        if(!ret) return ret;
                    } else {
                        emit_token_builder(buf, tok, cwae.t);
                    }
        // #if SIMPLE_DEBUG
                    Debug.Log("Keep going: " + buf);
        // #endif
                }
        // #if SIMPLE_DEBUG
                Debug.Log("Expanded function: " + cwae.buf + " to " + buf);
        // #endif
            } else {
                buf.Append(m.str_contents_buf);
                Debug.Log("Expanded object: " + name + " to " + buf);
            }
            return true;
        }

        const int TT_LAND = TT_CUSTOM+0;
        const int TT_LOR = TT_CUSTOM+1;
        const int TT_LTE = TT_CUSTOM+2;
        const int TT_GTE = TT_CUSTOM+3;
        const int TT_SHL = TT_CUSTOM+4;
        const int TT_SHR = TT_CUSTOM+5;
        const int TT_EQ = TT_CUSTOM+6;
        const int TT_NEQ = TT_CUSTOM+7;
        const int TT_LT = TT_CUSTOM+8;
        const int TT_GT = TT_CUSTOM+9;
        const int TT_BAND = TT_CUSTOM+10;
        const int TT_BOR = TT_CUSTOM+11;
        const int TT_XOR = TT_CUSTOM+12;
        const int TT_NEG = TT_CUSTOM+13;
        const int TT_PLUS = TT_CUSTOM+14;
        const int TT_MINUS = TT_CUSTOM+15;
        const int TT_MUL = TT_CUSTOM+16;
        const int TT_DIV = TT_CUSTOM+17;
        const int TT_MOD = TT_CUSTOM+18;
        const int TT_LPAREN = TT_CUSTOM+19;
        const int TT_RPAREN = TT_CUSTOM+20;
        const int TT_LNOT = TT_CUSTOM+21;
        const int TT_MAX = TT_CUSTOM+22;

        int TTINT(int X) {
            return X-TT_CUSTOM;
        }
        void TTENT(int[] list, int X, int Y) {
            list[TTINT(X)] = Y;
        }

        int[] bplist = null;

        int bp(int tokentype) {
            if (bplist == null) {
                bplist = new int[TT_MAX];
                TTENT(bplist, TT_LOR, 1 << 4);
                TTENT(bplist, TT_LAND, 1 << 5);
                TTENT(bplist, TT_BOR, 1 << 6);
                TTENT(bplist, TT_XOR, 1 << 7);
                TTENT(bplist, TT_BAND, 1 << 8);
                TTENT(bplist, TT_EQ, 1 << 9);
                TTENT(bplist, TT_NEQ, 1 << 9);
                TTENT(bplist, TT_LTE, 1 << 10);
                TTENT(bplist, TT_GTE, 1 << 10);
                TTENT(bplist, TT_LT, 1 << 10);
                TTENT(bplist, TT_GT, 1 << 10);
                TTENT(bplist, TT_SHL, 1 << 11);
                TTENT(bplist, TT_SHR, 1 << 11);
                TTENT(bplist, TT_PLUS, 1 << 12);
                TTENT(bplist, TT_MINUS, 1 << 12);
                TTENT(bplist, TT_MUL, 1 << 13);
                TTENT(bplist, TT_DIV, 1 << 13);
                TTENT(bplist, TT_MOD, 1 << 13);
                TTENT(bplist, TT_NEG, 1 << 14);
                TTENT(bplist, TT_LNOT, 1 << 14);
                TTENT(bplist, TT_LPAREN, 1 << 15);
        //      TTENT(bplist, TT_RPAREN, 1 << 15);
        //      TTENT(bplist, TT_LPAREN, 0);
                TTENT(bplist, TT_RPAREN, 0);
            }
            // Debug.Log("ttint " + TTINT(tokentype) + " / " + bplist.Length);
            if (TTINT(tokentype) < 0) {
                return 0;
            }
            if(TTINT(tokentype) < bplist.Length) return bplist[TTINT(tokentype)];
            return 0;
        }

        int charlit_to_int(string lit) {
            if(lit[1] == '\\') switch(lit[2]) {
                case '0': return 0;
                case 'n': return 10;
                case 't': return 9;
                case 'r': return 13;
                case 'x': {
                    return Byte.Parse(lit.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
                }
                default: return lit[2];
            }
            return lit[1];
        }

        int nud(Tokenizer t, Tokenizer.token tok, ref int err) {
            switch((int) tok.type) {
                case TT_IDENTIFIER: return 0;
                case TT_WIDECHAR_LIT:
                case TT_SQSTRING_LIT:  return charlit_to_int(t.buf.ToString());
                case TT_HEX_INT_LIT:
                case TT_OCT_INT_LIT:
                case TT_DEC_INT_LIT:
                    return (int)(Int64.Parse(t.buf.ToString()));
                case TT_NEG:   return ~ expr(t, bp(tok.type), ref err);
                case TT_PLUS:  return expr(t, bp(tok.type), ref err);
                case TT_MINUS: return - expr(t, bp(tok.type), ref err);
                case TT_LNOT:  return (0 == expr(t, bp(tok.type), ref err)) ? 1 : 0;
                case TT_LPAREN: {
                    // Debug.Log("nud paren before " + t.tokenizer_ftello());
                    int inner = expr(t, 0, ref err);
                    // Debug.Log("nud paren after " + t.tokenizer_ftello());
                    if(0!=expect(t, TT_RPAREN, new string []{")", ""}, out tok)) {
                        error("missing ')'", t, tok);
                        return 0;
                    }
                    return inner;
                }
                case TT_FLOAT_LIT:
                    error("floating constant in preprocessor expression", t, tok);
                    err = 1;
                    return 0;
                case TT_RPAREN:
                default:
                    error("unexpected tokens", t, tok);
                    err = 1;
                    return 0;
            }
        }

        int led(Tokenizer t, int left, Tokenizer.token tok, ref int err) {
            int right;
            // Debug.Log("led before " + t.tokenizer_ftello() + " " + tok.column);
            switch((int) tok.type) {
                case TT_LAND:
                case TT_LOR:
                    right = expr(t, bp(tok.type), ref err);
                    if(tok.type == TT_LAND) return ((left != 0) && (right != 0)) ? 1 : 0;
                    return ((left != 0) || (right != 0)) ? 1 : 0;
                case TT_LTE:  return left <= expr(t, bp(tok.type), ref err) ? 1 : 0;
                case TT_GTE:  return left >= expr(t, bp(tok.type), ref err) ? 1 : 0;
                case TT_SHL:  return left << expr(t, bp(tok.type), ref err);
                case TT_SHR:  return left >> expr(t, bp(tok.type), ref err);
                case TT_EQ:   return left == expr(t, bp(tok.type), ref err) ? 1 : 0;
                case TT_NEQ:  return left != expr(t, bp(tok.type), ref err) ? 1 : 0;
                case TT_LT:   return left <  expr(t, bp(tok.type), ref err) ? 1 : 0;
                case TT_GT:   return left >  expr(t, bp(tok.type), ref err) ? 1 : 0;
                case TT_BAND: return left &  expr(t, bp(tok.type), ref err);
                case TT_BOR:  return left |  expr(t, bp(tok.type), ref err);
                case TT_XOR:  return left ^  expr(t, bp(tok.type), ref err);
                case TT_PLUS: return left +  expr(t, bp(tok.type), ref err);
                case TT_MINUS:return left -  expr(t, bp(tok.type), ref err);
                case TT_MUL:  return left *  expr(t, bp(tok.type), ref err);
                case TT_DIV:
                case TT_MOD:
                    right = expr(t, bp(tok.type), ref err);
                    if(right == 0)  {
                        error("eval: div by zero", t, tok);
                        err = 1;
                    }
                    else if(tok.type == TT_DIV) return left / right;
                    else if(tok.type == TT_MOD) return left % right;
                    return 0;
                default:
                    error("eval: unexpect token", t, tok);
                    err = 1;
                    return 0;
            }
        }


        bool tokenizer_peek_next_non_ws(Tokenizer t, out Tokenizer.token tok)
        {
            bool ret;
            while (true) {
                ret = t.tokenizer_peek_token(out tok);
                if(is_whitespace_token(tok))
                    x_tokenizer_next(t, out tok);
                else break;
            }
            return ret;
        }

        int expr(Tokenizer t, int rbp, ref int err) {
            Tokenizer.token tok = new Tokenizer.token();
            bool ret = skip_next_and_ws(t, out tok);
            Debug.Log("expr before " + t.tokenizer_ftello() + " " + tok.column);
            if(tok.type == TT_EOF) return 0;
            int left = nud(t, tok, ref err);
            while (true) {
                ret = tokenizer_peek_next_non_ws(t, out tok);
                Debug.Log("expr loop " + t.tokenizer_ftello() + " " + tok.column + "," + tok.value + "," + rbp + ":" + tok.type + "," + bp(tok.type) + " ," + t.peeking + "," + t.peek_token.value + "," + t.buf);
                if(bp(tok.type) <= rbp) break;
                ret = t.tokenizer_next(out tok);
                Debug.Log("got next expr " + t.tokenizer_ftello() + " " + tok.column + "," + tok.value + "," + rbp + ":" + tok.type + "," + bp(tok.type) + " ," + t.peeking + "," + t.peek_token.value + "," + t.buf);
                if(tok.type == TT_EOF) break;
                left = led(t, left, tok, ref err);
            }
            return left;
        }

        bool do_eval(Tokenizer t, out int result) {
            t.tokenizer_register_custom_token(TT_LAND, "&&");
            t.tokenizer_register_custom_token(TT_LOR, "||");
            t.tokenizer_register_custom_token(TT_LTE, "<=");
            t.tokenizer_register_custom_token(TT_GTE, ">=");
            t.tokenizer_register_custom_token(TT_SHL, "<<");
            t.tokenizer_register_custom_token(TT_SHR, ">>");
            t.tokenizer_register_custom_token(TT_EQ, "==");
            t.tokenizer_register_custom_token(TT_NEQ, "!=");

            t.tokenizer_register_custom_token(TT_LT, "<");
            t.tokenizer_register_custom_token(TT_GT, ">");

            t.tokenizer_register_custom_token(TT_BAND, "&");
            t.tokenizer_register_custom_token(TT_BOR, "|");
            t.tokenizer_register_custom_token(TT_XOR, "^");
            t.tokenizer_register_custom_token(TT_NEG, "~");

            t.tokenizer_register_custom_token(TT_PLUS, "+");
            t.tokenizer_register_custom_token(TT_MINUS, "-");
            t.tokenizer_register_custom_token(TT_MUL, "*");
            t.tokenizer_register_custom_token(TT_DIV, "/");
            t.tokenizer_register_custom_token(TT_MOD, "%");

            t.tokenizer_register_custom_token(TT_LPAREN, "(");
            t.tokenizer_register_custom_token(TT_RPAREN, ")");
            t.tokenizer_register_custom_token(TT_LNOT, "!");

            int err = 0;
            result = expr(t, 0, ref err);
        #if DEBUG
            Debug.Log("eval result: " + result);
        #endif
            return err == 0;
        }

        bool evaluate_condition(Tokenizer t, ref int result, string[] visited) {
            bool ret, backslash_seen = false;
            token curr;
            StringBuilder bufp = new StringBuilder();
            int tflags = t.tokenizer_get_flags();
            t.tokenizer_set_flags(tflags | TF_PARSE_WIDE_STRINGS);
            ret = t.tokenizer_next(out curr);
            if(!ret) return ret;
            if(!is_whitespace_token(curr)) {
                error("expected whitespace after if/elif", t, curr);
                return false;
            }
            while (true) {
                ret = t.tokenizer_next(out curr);
                if(!ret) return ret;
                if(curr.type == TT_IDENTIFIER) {
                    if(!expand_macro(t, ref bufp, t.buf, -1, visited)) return false;
                } else if(curr.type == TT_SEP) {
                    if(curr.value == '\\')
                        backslash_seen = true;
                    else {
                        if(curr.value == '\n') {
                            if(!backslash_seen) break;
                        } else {
                            emit_token_builder(bufp, curr, t);
                        }
                        backslash_seen = false;
                    }
                } else {
                    emit_token_builder(bufp, curr, t);
                }
            }
            if(bufp.Length == 0) {
                error("#(el)if with no expression", t, curr);
                return false;
            }
        #if DEBUG
            Debug.Log("evaluating condition " + bufp);
        #endif
            Tokenizer t2 = new Tokenizer();
            tokenizer_from_file(t2, bufp);
            ret = do_eval(t2, out result);
            t.tokenizer_set_flags(tflags);
            return ret;
        }

        string[] directives = new string[]{"include", "error", "warning", "define", "undef", "if", "elif", "else", "ifdef", "ifndef", "endif", "line", "pragma", ""};

        public bool parse_file(string fn, string buf) {
            Tokenizer t = new Tokenizer();
            token curr;
            t.tokenizer_init(buf, TF_PARSE_STRINGS);
            t.tokenizer_set_filename(fn);
            t.tokenizer_register_marker(MT_MULTILINE_COMMENT_START, "/*"); /**/
            t.tokenizer_register_marker(MT_MULTILINE_COMMENT_END, "*/");
            t.tokenizer_register_marker(MT_SINGLELINE_COMMENT_START, "//");
            bool ret, newline=false;
            int ws_count = 0;

            int if_level = 0, if_level_active = 0, if_level_satisfied = 0;
            int xxdi = 0;

            while((ret = t.tokenizer_next(out curr)) && curr.type != TT_EOF) {
                newline = curr.column == 0;
                if(newline) {
                    ret = eat_whitespace(t, ref curr, out ws_count);
                    if(!ret) return ret;
                }
                if(curr.type == TT_EOF) break;
                if((if_level > if_level_active) && !(newline && is_char(curr, '#'))) continue;
                if(is_char(curr, '#')) {
                    if(!newline) {
                        error("stray #", t, curr);
                        return false;
                    }
                    int index = expect(t, TT_IDENTIFIER, directives, out curr);
        #if SIMPLE_DEBUG
                    Debug.Log("Preprocessor at " + t.filename + ":" + curr.line + " [" + if_level + "/" + if_level_active + "/" + if_level_satisfied + "]: #" + t.buf + " (type " + index + ")");
        #endif
                    if(index == -1) {
                        if((if_level > if_level_active)) continue;
                        error("invalid preprocessing directive", t, curr);
                        return false;
                    }
                    if((if_level > if_level_active)) switch(index) {
                        case 0: case 1: case 2: case 3: case 4:
                        case 11: case 12:
                            continue;
                        default: break;
                    }
                    switch(index) {
                    case 0:
                        ret = include_file(t);
                        if(!ret) return ret;
                        break;
                    case 1:
                        ret = emit_error_or_warning(t, true);
                        if(!ret) return ret;
                        break;
                    case 2:
                        ret = emit_error_or_warning(t, false);
                        if(!ret) return ret;
                        break;
                    case 3:
                        ret = parse_macro(t);
                        if(!ret) return ret;
                        break;
                    case 4:
                        if(!skip_next_and_ws(t, out curr)) return false;
                        if(curr.type != TT_IDENTIFIER) {
                            error("expected identifier", t, curr);
                            return false;
                        }
                        undef_macro(t.buf);
                        break;
                    case 5: // if
                    {
                        int newlevel = 0;
                        if((if_level_active == if_level)) {
                            string[] visited = new string[MAX_RECURSION];
                            int tmp = ret ? 1 : 0;
                            if(!evaluate_condition(t, ref tmp, visited)) return false;
                            newlevel = tmp;
                        }
                        if(if_level_active > if_level + 1) if_level_active = if_level + 1;
                        if(if_level_satisfied > if_level + 1) if_level_satisfied = if_level + 1;
                        if(newlevel != 0) if_level_active = if_level + 1;
                        else if(if_level_active == if_level + 1) if_level_active = if_level;
                        if(newlevel != 0 && if_level_active == if_level + 1) if_level_satisfied = if_level + 1;
                        if_level = if_level + 1;
                        break;
                    }
                    case 6: // elif
                        if((if_level_active == if_level-1) && if_level_satisfied < if_level) {
                            string[] visited = new string[MAX_RECURSION];
                            int tmp = ret ? 1 : 0;
                            if(!evaluate_condition(t, ref tmp, visited)) return false;
                            ret = tmp != 0;
                            if(ret) {
                                if_level_active = if_level;
                                if_level_satisfied = if_level;
                            }
                        } else if(if_level_active == if_level) {
                            --if_level_active;
                        }
                        break;
                    case 7: // else
                        if((if_level_active == if_level-1) && if_level_satisfied < if_level) {
                            if(true) {
                                if_level_active = if_level;
                                if_level_satisfied = if_level;
                            }
                        } else if(if_level_active == if_level) {
                            --if_level_active;
                        }
                        break;
                    case 8: // ifdef
                    case 9: // ifndef
                        if(!skip_next_and_ws(t, out curr) || curr.type == TT_EOF) return false;
                        ret = null != get_macro(t.buf);
                        if(index == 9) {
                            ret = !ret;
                        }
                        {
                            int newlevel = 0;
                            if ((if_level_active == if_level)){
                                newlevel = ret ? 1 : 0;
                            }
                            if(if_level_active > if_level + 1) if_level_active = if_level + 1;
                            if(if_level_satisfied > if_level + 1) if_level_satisfied = if_level + 1;
                            if(newlevel != 0) if_level_active = if_level + 1;
                            else if(if_level_active == if_level + 1) if_level_active = if_level;
                            if(newlevel != 0 && if_level_active == if_level + 1) if_level_satisfied = if_level + 1;
                            if_level = if_level + 1;
                        }
                        break;
                    case 10: // endif
                        if(if_level_active > if_level - 1) if_level_active = if_level - 1;
                        if(if_level_satisfied > if_level - 1) if_level_satisfied = if_level - 1;
                        if_level = if_level - 1;
                        break;
                    case 11: // line
                        ret = t.tokenizer_read_until("\n", true);
                        if(!ret) {
                            error("unknown", t, curr);
                            return false;
                        }
                        break;
                    case 12: // pragma
                        ret = t.tokenizer_read_until("\n", true);
                        // emit("#pragma", t, curr); // FIXME: pragma?
                        // while((ret = x_tokenizer_next(t, out curr)) && curr.type != TT_EOF) {
                        //     emit_token(curr, t);
                        //     if(is_char(curr, '\n')) break;
                        // }
                        // if(!ret) return ret;
                        break;
                    default:
                        break;
                    }
        #if SIMPLE_DEBUG
                    Debug.Log("Preprocessor done at " + t.filename + ":" + curr.line + " [" + if_level + "/" + if_level_active + "/" + if_level_satisfied + "]");
        #endif
                    continue;
                } else {
                    while(ws_count != 0) {
                        emit(" ", t, curr);
                        --ws_count;
                    }
                }
        #if DEBUG
                if(curr.type == TT_SEP)
                    Debug.Log(string.Format("(stdin:{0},{1}) ", curr.line, curr.column) + string.Format("separator: {0}", curr.value == '\n'? ' ' : (char)curr.value));
                else
                    Debug.Log(string.Format("(stdin:{0},{1}) ", curr.line, curr.column) + string.Format("{0}: {0}", tokentype_to_str(curr.type), t.buf));
        #endif
                if(curr.type == TT_IDENTIFIER) {
                    string[] visited = new string[MAX_RECURSION];
                    StringBuilder tmpout = new StringBuilder();
                    if(!expand_macro(t, ref tmpout, t.buf, 0, visited))
                        return false;
                    emit(tmpout.ToString(), t, curr); // FIXME: Expanded macro line number?
                } else {
                    emit_token(curr, t);
                }
                xxdi++;
                if (xxdi > 10000) {
                    break;
                }
            }
            if(if_level != 0) {
                error("unterminated #if", t, curr);
                return false;
            }
            return true;
        }

        public Preproc() {
            includedirs = new List<string>();
            cpp_add_includedir(".");
            macros = new Dictionary<string, macro>();
            macro m = new macro();
            m.arg_count = 1;
            add_macro("defined", m);
            m = new macro();
            m.arg_count = 0;
            m.objectlike = true;
            add_macro("__FILE__", m);
            m = new macro();
            m.arg_count = 0;
            m.objectlike = true;
            add_macro("__LINE__", m);
        }

        public void cpp_add_includedir(string includedir) {
            includedirs.Add((includedir));
        }

        public bool cpp_add_define(string mdecl) {
            FILE_container tmp = new FILE_container();
            tmp.buf = new StringBuilder();
            tmp.buf.Append(mdecl);
            tmp.buf.Append('\n');
            tmp.t = new Tokenizer();
            tokenizer_from_file(tmp.t, tmp.buf);
            bool ret = parse_macro(tmp.t);
            return ret;
        }

    }
}
