using System;
using Ror2Common;
using UnityEngine;
namespace Samus
{
    internal static class SamusPortrait
    {
        public static Texture2D Load()
        {
            if(!Plugin.AssetsReady)return null;
            var path=NativeLib.CString(Plugin.AssetsDir);
            if(MeleeNative.ssbm_portrait(path,0,out var info,null,0)!=1)throw new InvalidOperationException(MeleeNative.Error);
            var rgba=new byte[checked(info.width*info.height*4)];
            if(MeleeNative.ssbm_portrait(path,0,out info,rgba,(uint)rgba.Length)!=1)throw new InvalidOperationException(MeleeNative.Error);
            return Portrait.FromRgba("Melee Samus original CSS grid portrait",(int)info.width,(int)info.height,rgba);
        }
    }
}

