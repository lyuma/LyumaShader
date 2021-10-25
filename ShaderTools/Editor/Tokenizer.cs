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
    public class Tokenizer
    {

        public const int EOF = -1;

        class tokenizer_getc_buf {
            public string buf = "";
            public int cnt = 0;
        };

        public const int MAX_TOK_LEN = 4096;

        // enum markertype {
        public const int MT_SINGLELINE_COMMENT_START = 0;
        public const int MT_MULTILINE_COMMENT_START = 1;
        public const int MT_MULTILINE_COMMENT_END = 2;
        public const int MT_MAX = MT_MULTILINE_COMMENT_END;
        // }

        public const int MAX_CUSTOM_TOKENS = 32;

        //enum tokentype {
        public const int TT_IDENTIFIER = 1;
        public const int TT_SQSTRING_LIT = 2;
        public const int TT_DQSTRING_LIT = 3;
        public const int TT_ELLIPSIS = 4;
        public const int TT_HEX_INT_LIT = 5;
        public const int TT_OCT_INT_LIT = 6;
        public const int TT_DEC_INT_LIT = 7;
        public const int TT_FLOAT_LIT = 8;
        public const int TT_SEP = 9;
        /* errors and similar */
        public const int TT_UNKNOWN = 10;
        public const int TT_OVERFLOW = 11;
        public const int TT_WIDECHAR_LIT = 12;
        public const int TT_WIDESTRING_LIT = 13;
        public const int TT_EOF = 14;
        public const int TT_CUSTOM = 1000; /* start user defined tokentype values */
        
        public struct token {
            public int type; // tokentype
            public int line;
            public int column;
            public int value;
        };

        // enum tokenizer_flags {
        public const int TF_PARSE_STRINGS = 1 << 0;
        public const int TF_PARSE_WIDE_STRINGS = 1 << 1;
        // };

        public int line;
        public int column;
        public int flags;
        public int custom_count;
        public bool peeking;
        public string[] custom_tokens = new string[MAX_CUSTOM_TOKENS];
        public string buf = ""; //new StringBuilder();
        tokenizer_getc_buf getc_buf = new tokenizer_getc_buf();
        public string[] marker = new string[MT_MAX+1];
        public string filename;
        public token peek_token;

        public void tokenizer_set_filename(string fn) {
            filename = fn;
        }

        public int tokenizer_ftello() {
            return getc_buf.cnt;
        }

        public int tokenizer_ungetc(int c)
        {
            --getc_buf.cnt;
            return c;
        }
        public int tokenizer_getc()
        {
            if (getc_buf.cnt >= getc_buf.buf.Length) {
                return EOF;
            }
            int c = getc_buf.buf[getc_buf.cnt];
            ++getc_buf.cnt;
            return c;
        }

        public int tokenizer_peek() {
            if(peeking) return peek_token.value;
            int ret = tokenizer_getc();
            if(ret != EOF) tokenizer_ungetc(ret);
            return ret;
        }

        public bool tokenizer_peek_token(out token tok) {
            if (peeking) { // Lyuma bugfix!
                tok = new token();
                tok.column = peek_token.column;
                tok.line = peek_token.line;
                tok.type = peek_token.type;
                tok.value = peek_token.value;
                return true;
            }
            bool ret = tokenizer_next(out tok);
            peek_token = new token();
            peek_token.column = tok.column;
            peek_token.line = tok.line;
            peek_token.type = tok.type;
            peek_token.value = tok.value;
            peeking = true;
            return ret;
        }

        public void tokenizer_register_custom_token(int tokentype, string str) {
            if(!(tokentype >= TT_CUSTOM && tokentype < TT_CUSTOM + MAX_CUSTOM_TOKENS)) {
                Debug.LogError("Wrong tokentype " + tokentype);
            }
            int pos = tokentype - TT_CUSTOM;
            custom_tokens[pos] = str;
            if(pos+1 > custom_count) custom_count = pos+1;
        }

        public string tokentype_to_str(int tt) { // tokentype
            switch(tt) {
                case TT_IDENTIFIER: return "iden";
                case TT_WIDECHAR_LIT: return "widechar";
                case TT_WIDESTRING_LIT: return "widestring";
                case TT_SQSTRING_LIT: return "single-quoted string";
                case TT_DQSTRING_LIT: return "double-quoted string";
                case TT_ELLIPSIS: return "ellipsis";
                case TT_HEX_INT_LIT: return "hexint";
                case TT_OCT_INT_LIT: return "octint";
                case TT_DEC_INT_LIT: return "decint";
                case TT_FLOAT_LIT: return "float";
                case TT_SEP: return "separator";
                case TT_UNKNOWN: return "unknown";
                case TT_OVERFLOW: return "overflow";
                case TT_EOF: return "eof";
            }
            return "????";
        }

        bool has_f_tail(string p) {
            return p.EndsWith("f", StringComparison.CurrentCultureIgnoreCase) || p.EndsWith("h", StringComparison.CurrentCultureIgnoreCase);
        }
        bool has_ul_tail(string p) {
            return p.EndsWith("u", StringComparison.CurrentCultureIgnoreCase);
        }

        bool is_hex_int_literal(string s) {
            int i = 0;
            if(is_plus_or_minus(s[0])) {
                i++;
            }
            while (i < s.Length && Char.IsWhiteSpace(s[i])) {
                i++;
            }
            if (i > s.Length - 4) {
                return false;
            }
            if (s[i] == '0' && (s[i + 1] == 'x' || s[i + 1] == 'X')) {
                Int64 ret;
                return Int64.TryParse(s.Substring(i + 2, s.Length - (has_ul_tail(s) ? 1 : 0) - (i + 2)), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ret);
            }
            return false;
        }

        bool is_plus_or_minus(char c) {
            return c == '-' || c == '+';
        }

        bool is_dec_int_literal(string str) {
            string s = has_ul_tail(str) ? str.Substring(0, str.Length - 1) : str;
            long ret;
            return Int64.TryParse(s, out ret);
        }

        bool is_float_literal(string str) {
            string s = has_f_tail(str) ? str.Substring(0, str.Length - 1) : str;
            float res;
            return float.TryParse(s, out res);
        }

        int is_valid_float_until(string s, int until) {
            bool got_digits = false, got_dot = false;
            int off = 0;
            while(off < until) {
                if(Char.IsDigit(s[off])) got_digits = true;
                else if(s[off] == '.') {
                    if(got_dot) return 0;
                    got_dot = true;
                } else return 0;
                ++off;
            }
            return (got_digits?1:0) | ((got_dot?1:0) << 1);
        }

        bool is_oct_int_literal(string s) {
            int i = 0;
            if(s[0] == '-') i++;
            if(s[0] != '0') return false;
            while(i < s.Length) {
                if(s[i] < '0' || s[i] > '7') return false;
                i++;
            }
            return true;
        }

        bool is_identifier(string s) {
            if (!Char.IsLetter(s[0]) && s[0] != '_') {
                return false;
            }
            for(int i = 1; i < s.Length; i++) {
                if(!Char.IsLetterOrDigit(s[i]) && s[i] != '_') {
                    return false;
                }
            }
            return true;
        }

        int categorize(string s) { // tokentype
            if(is_hex_int_literal(s)) return TT_HEX_INT_LIT;
            if(is_dec_int_literal(s)) return TT_DEC_INT_LIT;
            if(is_oct_int_literal(s)) return TT_OCT_INT_LIT;
            if(is_float_literal(s)) return TT_FLOAT_LIT;
            if(is_identifier(s)) return TT_IDENTIFIER;
            return TT_UNKNOWN;
        }


        bool is_sep(char c) {
            if (c == '_') {
                return false;
            }
            return !Char.IsLetterOrDigit(c);
            // return Char.IsPunctuation(c) || Char.IsSeparator(c) || Char.IsWhiteSpace(c);
        }

        bool apply_coords(ref token outt, int end, bool retval) {
            outt.line = line;
            int len = end;
            outt.column = column - len;
            // if(len + 1 >= bufsize) {
            //     outt.type = TT_OVERFLOW;
            //     return false;
            // }
            return retval;
        }

        int assign_bufchar(int s, int c) {
            column++;
            buf += ((char)c);
            return s + 1;
        }

        bool get_string(char quote_char, out token tout, bool wide) {
            int s = 1;
            bool escaped = false;
            int end = MAX_TOK_LEN - 2;
            tout = new token();
            while(s < end) {
                int c = tokenizer_getc();
                if(c == EOF) {
                    tout.type = TT_EOF;
                    // buf = 0;
                    return apply_coords(ref tout, s, false);
                }
                if(c == '\\') {
                    c = tokenizer_getc();
                    if(c == '\n') continue;
                    tokenizer_ungetc(c);
                    c = '\\';
                }
                if(c == '\n') {
                    if(escaped) {
                        escaped = false;
                        continue;
                    }
                    tokenizer_ungetc(c);
                    tout.type = TT_UNKNOWN;
                    s = assign_bufchar(s, 0);
                    return apply_coords(ref tout, s, false);
                }
                if(!escaped) {
                    if(c == quote_char) {
                        s = assign_bufchar(s, c);
                        // *s = 0;
                        //s = assign_bufchar(t, s, 0);
                        if(!wide)
                            tout.type = (quote_char == '"'? TT_DQSTRING_LIT : TT_SQSTRING_LIT);
                        else
                            tout.type = (quote_char == '"'? TT_WIDESTRING_LIT : TT_WIDECHAR_LIT);
                        return apply_coords(ref tout, s, true);
                    }
                    if(c == '\\') escaped = true;
                } else {
                    escaped = false;
                }
                s = assign_bufchar(s, c);
            }
            // buf[MAX_TOK_LEN-1] = 0;
            tout.type = TT_OVERFLOW;
            return apply_coords(ref tout, s, false);
        }

        /* if sequence found, next tokenizer call will point after the sequence */
        bool sequence_follows(int c, string which)
        {
            if(which.Length == 0) return false;
            int i = 0;
            while(i < which.Length && c == which[i]) {
                if(++i >= which.Length) break;
                c = tokenizer_getc();
            }
            if(i >= which.Length) return true;
            while(i > 0) {
                tokenizer_ungetc(c);
                c = which[--i];
            }
            return false;
        }

        public bool tokenizer_skip_chars(string chars, out int count) {
            if(peeking) {
                Debug.LogError("Assertion failure: skip chars while peeking");
            }
            int c;
            count = 0;
            while(true) {
                c = tokenizer_getc();
                if(c == EOF) return false;
                int s = 0;
                bool match = false;
                while(s < chars.Length) {
                    if(c==chars[s]) {
                        ++(count);
                        match = true;
                        break;
                    }
                    ++s;
                }
                if(!match) {
                    tokenizer_ungetc(c);
                    return true;
                }
            }

        }

        public bool tokenizer_read_until(string marker, bool stop_at_nl)
        {
            int c;
            bool marker_is_nl = marker == "\n";
            int s = 0;
            buf = ""; //.Clear();
            while(true) {
                c = tokenizer_getc();
                if(c == EOF) {
                    // *s = 0;
                    return false;
                }
                if(c == '\n') {
                    line++;
                    column = 0;
                    if(stop_at_nl) {
                        // *s = 0;
                        if(marker_is_nl) return true;
                        return false;
                    }
                }
                if(!sequence_follows(c, marker))
                    s = assign_bufchar(s, c);
                else
                    break;
            }
            // *s = 0;
            int i;
            for(i=marker.Length; i > 0; )
                tokenizer_ungetc(marker[--i]);
            return true;
        }
        bool ignore_until(string marker, int col_advance, bool stop_at_newline=false)
        {
            column += col_advance;
            int c;
            do {
                c = tokenizer_getc();
                if(c == EOF) return false;
                if(c == '\n') {
                    if (stop_at_newline) {
                        tokenizer_ungetc(c); // Lyuma bugfix
                        /*
                        #define FOO bar // some comment
                        #endif
                        */
                        return true;
                    }
                    line++;
                    column = 0;
                } else column++;
            } while(!sequence_follows(c, marker));
            column += (marker.Length)-1;
            return true;
        }

        public void tokenizer_skip_until(string marker)
        {
            ignore_until(marker, 0, marker == "\n");
        }

        public bool tokenizer_next(out token tout) {
            int s = 0;
            int c = 0;
            if(peeking) {
                tout = new token();
                tout.value = peek_token.value;
                tout.column = peek_token.column;
                tout.line = peek_token.line;
                tout.type = peek_token.type;
                peeking = false;
                peek_token = new token();
                return true;
            }
            buf = ""; //.Clear();
            tout = new token();
            tout.value = 0;
            while(true) {
                c = tokenizer_getc();
                if(c == EOF) {
                    break;
                }

                /* components of multi-line comment marker might be terminals themselves */
                if(sequence_follows(c, marker[MT_MULTILINE_COMMENT_START])) {
                    ignore_until(marker[MT_MULTILINE_COMMENT_END], marker[MT_MULTILINE_COMMENT_START].Length);
                    continue;
                }
                if(sequence_follows(c, marker[MT_SINGLELINE_COMMENT_START])) {
                    ignore_until("\n", marker[MT_SINGLELINE_COMMENT_START].Length, true);
                    continue;
                }
                if(is_sep((char)c)) {
                    if(s != 0 && c == '\\' && !Char.IsWhiteSpace(buf[s-1])) {
                        c = tokenizer_getc();
                        if(c == '\n') continue;
                        tokenizer_ungetc(c);
                        c = '\\';
                    } else if(is_plus_or_minus((char)c) && s > 1 &&
                        (buf[s-1] == 'E' || buf[s-1] == 'e' || buf[s-1] == 'F' || buf[s-1] == 'f' || buf[s-1] == 'H' || buf[s-1] == 'h') && is_valid_float_until(buf, s-1) != 0) {
                        goto process_char;
                    } else if(c == '.' && s != 0 && is_valid_float_until(buf, s) == 1) {
                        goto process_char;
                    } else if(c == '.' && s == 0) {
                        bool jump = false;
                        c = tokenizer_getc();
                        if(Char.IsDigit((char)c)) jump = true;
                        tokenizer_ungetc(c);
                        c = '.';
                        if(jump) goto process_char;
                    }
                    tokenizer_ungetc(c);
                    break;
                }
                if((flags & TF_PARSE_WIDE_STRINGS) != 0 && s == 0 && c == 'L') {
                    c = tokenizer_getc();
                    tokenizer_ungetc(c);
                    tokenizer_ungetc('L');
                    if(c == '\'' || c == '\"') break;
                }

        process_char:;
                s = assign_bufchar(s, c);
                if(column + 1 >= MAX_TOK_LEN) {
                    tout.type = TT_OVERFLOW;
                    return apply_coords(ref tout, s, false);
                }
            }
            if(s == 0) {
                if(c == EOF) {
                    tout.type = TT_EOF;
                    return apply_coords(ref tout, s, true);
                }

                bool wide = false;
                c = tokenizer_getc();
                if((flags & TF_PARSE_WIDE_STRINGS) != 0 && c == 'L') {
                    c = tokenizer_getc();
                    if(!(c == '\'' || c == '\"')) {
                        Debug.LogError("bad c " + c);
                    }
                    wide = true;
                    goto string_handling;
                } else if (c == '.' && sequence_follows(c, "...")) {
                    buf = "..."; //.Clear();
                    //buf.Append("...");
                    tout.type = TT_ELLIPSIS;
                    return apply_coords(ref tout, s+3, true);
                }

                {
                    int i;
                    for(i = 0; i < custom_count; i++)
                        if(sequence_follows(c, custom_tokens[i])) {
                            string p = custom_tokens[i];
                            int pi = 0;
                            while(pi < p.Length) {
                                s = assign_bufchar(s, p[pi]);
                                pi++;
                            }
                            // *s = 0;
                            tout.type = TT_CUSTOM + i;
                            return apply_coords(ref tout, s, true);
                        }
                }

        string_handling:
                s = assign_bufchar(s, c);
                // *s = 0;
                //s = assign_bufchar(t, s, 0);
                if(c == '"' || c == '\'')
                    if((flags & TF_PARSE_STRINGS) != 0) return get_string((char)c, out tout, wide);
                tout.type = TT_SEP;
                tout.value = c;
                if(c == '\n') {
                    apply_coords(ref tout, s, true);
                    line++;
                    column=0;
                    return true;
                }
                return apply_coords(ref tout, s, true);
            }
            //s = assign_bufchar(t, s, 0);
            // s = 0;
            tout.type = categorize(buf);
            return apply_coords(ref tout, s, tout.type != TT_UNKNOWN);
        }

        public void tokenizer_set_flags(int flags) {
            this.flags = flags;
        }

        public int tokenizer_get_flags() {
            return flags;
        }

        public void tokenizer_init(string file_contents, int flags) {
            if (file_contents.Length < -1) {
                Debug.Log("wat");
            }
            for (int i = 0; i < marker.Length; i++) {
                marker[i] = "";
            }
            getc_buf.buf = file_contents;
            getc_buf.cnt = 0;
            line = 1;
            this.flags = flags;
        }

        public void tokenizer_register_marker(int mt, string marker)
        {
            this.marker[mt] = marker;
        }

        public void tokenizer_rewind() {
            int flags = this.flags;
            string fn = filename;
            tokenizer_init(getc_buf.buf, flags);
            tokenizer_set_filename(fn);
        }

    }
}