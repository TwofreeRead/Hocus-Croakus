using FIMSpace.FEyes;
using System.Collections.Generic;
using UnityEngine;

public partial class FEyesAnimator_Editor : UnityEditor.Editor
{
    public static Texture2D _TexEyesAnimIcon { get { if (__texEyesAnimIcon != null) return __texEyesAnimIcon; __texEyesAnimIcon = Resources.Load<Texture2D>("Eyes Animator/EyesAnimator_IconSmall"); return __texEyesAnimIcon; } }
    private static Texture2D __texEyesAnimIcon = null;
    public static Texture2D _TexEyeIcon { get { if (__texEyeIcon != null) return __texEyeIcon; __texEyeIcon = Resources.Load<Texture2D>("Eyes Animator/EyeIcon"); return __texEyeIcon; } }
    private static Texture2D __texEyeIcon = null;
    public static Texture2D _TexBlinkIcon { get { if (__texBlinkIcon != null) return __texBlinkIcon; __texBlinkIcon = Resources.Load<Texture2D>("Eyes Animator/BlinkingIcon"); return __texBlinkIcon; } }
    private static Texture2D __texBlinkIcon = null;
    public static Texture2D _TexEyeRandomIcon { get { if (__texEyeRandomIcon != null) return __texEyeRandomIcon; __texEyeRandomIcon = Resources.Load<Texture2D>("Eyes Animator/EyeRandom"); return __texEyeRandomIcon; } }
    private static Texture2D __texEyeRandomIcon = null;

    public static Texture2D _TexEyeLidUpIcon { get { if (__texEyeLidUpIcon != null) return __texEyeLidUpIcon; __texEyeLidUpIcon = Resources.Load<Texture2D>("Eyes Animator/EyeLidUp"); return __texEyeLidUpIcon; } }
    private static Texture2D __texEyeLidUpIcon = null;
    public static Texture2D _TexEyeLidDownIcon { get { if (__texEyeLidDownIcon != null) return __texEyeLidDownIcon; __texEyeLidDownIcon = Resources.Load<Texture2D>("Eyes Animator/EyeLidDown"); return __texEyeLidDownIcon; } }
    private static Texture2D __texEyeLidDownIcon = null;

    public static Texture2D _TexEyeFollowIcon { get { if (__texEyeFollowIcon != null) return __texEyeFollowIcon; __texEyeFollowIcon = Resources.Load<Texture2D>("Eyes Animator/EyeFollow"); return __texEyeFollowIcon; } }
    private static Texture2D __texEyeFollowIcon = null;

    private FEyesAnimator Get { get { if (_get == null) _get = target as FEyesAnimator; return _get; } }
    private FEyesAnimator _get;

    private static UnityEngine.Object _manualFile;
    private Color c;

    public List<SkinnedMeshRenderer> skins;
    SkinnedMeshRenderer largestSkin;
    Animator animator;
    Animation animation;

    bool drawRandomSettings = true;
    bool drawLagSettings = true;


    /// <summary>
    /// Trying to deep find skinned mesh renderer
    /// </summary>
    private void FindComponents()
    {
        if (skins == null) skins = new List<SkinnedMeshRenderer>();

        foreach (var t in Get.transform.GetComponentsInChildren<Transform>())
        {
            SkinnedMeshRenderer s = t.GetComponent<SkinnedMeshRenderer>(); if (s) skins.Add(s);
            if (!animator) animator = t.GetComponent<Animator>();
            if (!animator) if (!animation) animation = t.GetComponent<Animation>();
        }

        if ((skins != null && largestSkin != null) && (animator != null || animation != null)) return;

        if (Get.transform != Get.transform)
        {
            foreach (var t in Get.transform.GetComponentsInChildren<Transform>())
            {
                SkinnedMeshRenderer s = t.GetComponent<SkinnedMeshRenderer>(); if (!skins.Contains(s)) if (s) skins.Add(s);
                if (!animator) animator = t.GetComponent<Animator>();
                if (!animator) if (!animation) animation = t.GetComponent<Animation>();
            }
        }

        // Searching in parent
        if (skins.Count == 0)
        {
            Transform lastParent = Get.transform;

            while (lastParent != null)
            {
                if (lastParent.parent == null) break;
                lastParent = lastParent.parent;
            }

            foreach (var t in lastParent.GetComponentsInChildren<Transform>())
            {
                SkinnedMeshRenderer s = t.GetComponent<SkinnedMeshRenderer>(); if (!skins.Contains(s)) if (s) skins.Add(s);
                if (!animator) animator = t.GetComponent<Animator>();
                if (!animator) if (!animation) animation = t.GetComponent<Animation>();
            }
        }

        if (skins.Count > 1)
        {
            largestSkin = skins[0];
            for (int i = 1; i < skins.Count; i++)
                if (skins[i].bones.Length > largestSkin.bones.Length)
                    largestSkin = skins[i];
        }
        else
            if (skins.Count > 0) largestSkin = skins[0];

    }


    /// <summary>
    /// Searching through component's owner to find head or neck bone
    /// </summary>
    private void FindHeadBone(FEyesAnimator eyesAnimator)
    {
        eyesAnimator.FindHeadBone();
    }

    protected virtual void FindingEyes(FEyesAnimator eyesAnimator)
    {
        eyesAnimator.FindingEyes();
    }

}
