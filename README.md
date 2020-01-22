# Lyuma's Shaders

This is a collection of editor scripts and shaders for VRChat. Currently, the repository focuses on a 2d effect. More shaders and scripts will be coming.

# Contents

## Skinning shaders:

### ShaderSkin/BoneBlendDumper.shader

Needed to dump bone positions and viseme values into pixels, to be picked up either by GrabPass or a camera.

### ShaderSkin/OldSkin

Legacy skinning systems from November 2018.

### ShaderSkin/NewShaderSkin

GrabPass based skinning system

### ShaderSkin/CameraSystem

Unused simple camera system - works like GrabPass

### ShaderSkin/CameraSystemv2

Full camera based system - captures bone rotations, and recomputes transform hierarchy.

### ShaderSkin/LiteSkinning

Example lite skinning system, weighting each polygon to three bone positions passed into geometry shader.
Not really useful.... more examples of this to come.

## Editor scripts:

TODO: document

## 2d flat shader generator and standard simple:

* LyumaShader/DropShadowLite.shader: This renders a flat drop shadow behind the object. Usually used by making a duplicate of your material or mesh and applying this shader to the duplicate. It contains its own offset, so it is ok to apply to a SkinnedMeshRenderer of an existing armature.
* Shader2d/StandardSimple.shader: This is a replica of the unity Standard shader, including the same header files.
* Shader2d/StandardSimple_2d.shader: This is a sample of a generated 2d shader based on Standard.
* Shader2d/Shader2d.cginc: This file will be included by all generated shaders. Note: unity does not recompile other shaders when this file is modified. Beware that changes you make here may not be reflected until a time far in the future when unity re-imports your assets.
* Shader2d/Editor/Shader2dScript.cs: This adds a context menu item to shaders and materials to generate a 2d version.

# Conclusion

That's it for now. In the future, I may include screenshots, samples, etc.

# Contact

Feel free to contact me by opening an issue, on Discord, or by email at xnlyuma@gmail.com
