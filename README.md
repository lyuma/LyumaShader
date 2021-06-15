# Lyuma's Shaders

This is a collection of editor scripts and shaders for Unity or VRChat. Currently, the repository focuses on a 2d effect. More shaders and scripts will be coming.

To download, expand the Releases link, and download the unitypackage... Or, click the green Code button and Download ZIP. Then extract into your unity Assets folder.

# Waifu2d

This feature, "Make 2d", will flatten any material onto a vertical plane, by modifying the shader.

![access Waifu2d from the Material context menu](Waifu2d/waifu2d_preview.png)\
*Featured: Amanatsu by komado showing Silent's Crosstone, Xiexe's XSToon 3.0.1 and Poiyomi Toon v7.1.53*

To access, select all the materials you wish to convert, then use the gear menu to select **Make 2d** - to switch materials back to 3d mode, select **Revert to 3d**. It works well when combined with an extremely thick outline with solid color.

## How to configure and animate 2d effects

The material inspector is disabled once you are in 2d mode. While in this mode, you can configure the plane to face one direction (Locked to a 2d axis determined by Facing direction), or billboarded to the player's camera for a sprite game feel. Feel free to revert to 3d to make more material changes, or edit the sliders directly.

You can animate the _2d_coef and _lock_2d_axis material values for switching between 2d, billboard, and 3d modes. You must do this on every mesh to avoid clipping. You may want to also animate the outline width to be smaller when in 3d.

Note that this shader cheats and produces falsified depth values to minimize Z-fighting. However, if you wish to mix parts in 2d and 3d, you can adjust the "Squash Z" value to some value usually around 0.95 or 0.975 so that they line up.

## Other notes about 2d

This shader requires that *all* meshes have the same **Root Bone** slot set in the SkinnedMeshRenderer. Changing the Root Bone is mostly safe, but you must adjust the Mesh Bounds to compensate. If you have any meshes which are not skinned, they will not align to the same 2d plane and may look unusual. This is best fixed from within blender by combining meshes.

"See self in 3d" has been removed. If you need this functionality use the Avatar 3.0 IsLocal parameter.

Note that this includes a copy of the built-in Standard shader for metallic setup only. Specular setup is not implemented yet, but would be easy to add if requested.

# Other Contents

* LyumaShader/DropShadowLite.shader: This renders a flat drop shadow behind the object. Usually used by making a duplicate of your material or mesh and applying this shader to the duplicate. It contains its own offset, so it is ok to apply to a SkinnedMeshRenderer of an existing armature.

# Conclusion

That's it for now. In the future, I may include screenshots, samples, etc.

I also have a collection of GPU skinning examples in the `newshaderskin` branch.

Also, check out my gists: https://gist.github.com/lyuma

# Contact

Feel free to contact me by opening an issue, Lyuma on Discord (#0781), or by email at xnlyuma@gmail.com
