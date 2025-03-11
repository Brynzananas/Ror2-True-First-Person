using EmotesAPI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TrueFirstPerson
{
    public static class EmoteCompatability
    {
        public const string customEmotesApiGUID = "com.weliveinasociety.CustomEmotesAPI";
        public static string CurrentEmote()
        {
            if (CustomEmotesAPI.localMapper)
            {
                return CustomEmotesAPI.localMapper.currentClipName;
            }
            else
            {
                return "none";
            }
        }
        public static Quaternion GetHeadRotation()
        {
            if (CustomEmotesAPI.localMapper)
            {
                return CustomEmotesAPI.localMapper.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head).rotation;
            }
            else
            {
                return Quaternion.identity;
            }
        }
        public static Transform GetHeadBone()
        {
            if (CustomEmotesAPI.localMapper)
            {
                return CustomEmotesAPI.localMapper.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            }
            else
            {
                return null;
            }
        }
    }
}
