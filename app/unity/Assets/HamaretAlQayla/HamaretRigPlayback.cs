using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

internal sealed class HamaretRigPlayback : IDisposable
{
    private const string ModelPath = "Hamaret/HamaretRiggedReview";
    private const string FittedModelPath = "Hamaret/MetaHuman/HamaretFittedWalk";
    private readonly PlayableGraph graph;
    private readonly List<Material> materials = new List<Material>();
    private readonly AnimationMixerPlayable mixer;
    private readonly AnimationClipPlayable[] clips;
    private readonly float[] lengths;
    private readonly GameObject model;
    private readonly SkinnedMeshRenderer[] skinParts;
    private readonly MaterialPropertyBlock dissolve = new MaterialPropertyBlock();
    private float gaitBlend, nextBlink, blinkBegin = -10f;
    private readonly SkinnedMeshRenderer face;
    private readonly int blinkIndex = -1;
    private readonly Transform leftEye, rightEye;
    private readonly Quaternion leftRest, rightRest;
    private readonly GroundedHoof leftHoof, rightHoof;
    private readonly bool fitted;

    internal HamaretRigPlayback(Transform parent)
    {
        var fittedAsset = Resources.Load<GameObject>(FittedModelPath);
        var walk = Array.Find(Resources.LoadAll<AnimationClip>(FittedModelPath),
            clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (fittedAsset != null && walk != null)
        {
            fitted = true;
            model = Object.Instantiate(fittedAsset, parent, false);
            model.name = "Rigged Hamaret";
            model.transform.localScale *= 1.13f;
            skinParts = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Dress(model);
            foreach (var renderer in skinParts)
            {
                int index = renderer.sharedMesh.GetBlendShapeIndex("Blink");
                if (index < 0)
                    continue;
                face = renderer;
                blinkIndex = index;
                break;
            }
            nextBlink = Time.time + 1.45f;
            var fittedAnimator = model.GetComponent<Animator>();
            if (fittedAnimator == null)
                fittedAnimator = model.AddComponent<Animator>();
            fittedAnimator.applyRootMotion = false;
            fittedAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Hamaret fitted rig");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var fittedWalk = AnimationClipPlayable.Create(graph, walk);
            fittedWalk.SetApplyFootIK(false);
            clips = new[] { fittedWalk };
            lengths = new[] { walk.length };
            AnimationPlayableOutput.Create(graph, "Walking pose", fittedAnimator).SetSourcePlayable(fittedWalk);
            graph.Play();
            Pose(0f, 0f);
            return;
        }

        var asset = Resources.Load<GameObject>(ModelPath);
        if (asset == null)
            throw new InvalidOperationException("The packaged Hamaret rig could not be loaded.");

        string[] poses = { "breathing idle", "hoof strike", "transformation gesture", "walk review" };
        var imported = Resources.LoadAll<AnimationClip>(ModelPath);
        var selected = new AnimationClip[poses.Length];
        for (int i = 0; i < poses.Length; i++)
        {
            string suffix = poses[i];
            selected[i] = Array.Find(imported, clip => clip.name.EndsWith(suffix, StringComparison.Ordinal));
            if (selected[i] == null)
                throw new InvalidOperationException("Missing Hamaret animation: " + suffix);
        }

        model = Object.Instantiate(asset, parent, false);
        model.name = "Rigged Hamaret";
        Dress(model);
        skinParts = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinParts)
        {
            int index = renderer.sharedMesh.GetBlendShapeIndex("Blink");
            if (index < 0)
                continue;
            face = renderer;
            blinkIndex = index;
            break;
        }
        foreach (var part in model.GetComponentsInChildren<Transform>(true))
        {
            if (part.name == "eye.L") leftEye = part;
            if (part.name == "eye.R") rightEye = part;
        }
        if (leftEye != null) leftRest = leftEye.localRotation;
        if (rightEye != null) rightRest = rightEye.localRotation;
        leftHoof = FindHoof(model.transform, ".L");
        rightHoof = FindHoof(model.transform, ".R");
        nextBlink = Time.time + 1.45f;
        SetHuman(false);
        var animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("Hamaret character");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        mixer = AnimationMixerPlayable.Create(graph, poses.Length);
        clips = new AnimationClipPlayable[poses.Length];
        lengths = new float[poses.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i] = AnimationClipPlayable.Create(graph, selected[i]);
            clips[i].SetApplyFootIK(false);
            graph.Connect(clips[i], 0, mixer, i);
            lengths[i] = selected[i].length;
        }
        AnimationPlayableOutput.Create(graph, "Hamaret pose", animator).SetSourcePlayable(mixer);
        graph.Play();
        Pose(0f, 0f);
    }

    internal void Pose(float strike, float flourish, bool walking = false, float traveled = 0f)
    {
        gaitBlend = Mathf.MoveTowards(gaitBlend, walking && flourish <= 0f ? 1f : 0f, Time.deltaTime * 5f);
        if (fitted)
        {
            float phase = walking ? Mathf.Repeat(traveled / .85f, 1f) : .14f;
            clips[0].SetTime(phase * lengths[0]);
            graph.Evaluate(0f);
            Blink();
            dissolve.SetFloat("_Gait", gaitBlend);
            foreach (var renderer in skinParts)
                renderer.SetPropertyBlock(dissolve);
            return;
        }
        mixer.SetInputWeight(0, strike > 0f || flourish > 0f ? 0f : 1f - gaitBlend);
        mixer.SetInputWeight(1, strike > 0f ? 1f : 0f);
        mixer.SetInputWeight(2, flourish > 0f ? 1f : 0f);
        clips[0].SetTime(Mathf.Repeat(Time.time, lengths[0]));
        clips[1].SetTime(Mathf.Clamp01(strike) * lengths[1]);
        clips[2].SetTime(Mathf.Clamp01(flourish) * lengths[2]);
        mixer.SetInputWeight(3, flourish > 0f ? 0f : gaitBlend);
        float stride = Mathf.Repeat(traveled / 1.08f, 1f);
        clips[3].SetTime(stride * lengths[3]);
        graph.Evaluate(0f);
        bool lockFeet = walking && gaitBlend > .8f && flourish <= 0f;
        leftHoof?.Update(stride, model.transform.position.y, lockFeet);
        rightHoof?.Update(Mathf.Repeat(stride + .5f, 1f), model.transform.position.y, lockFeet);
        Blink();
        var gaze = Quaternion.Euler(Mathf.Sin(Time.time * .91f) * 1.6f,
            Mathf.Sin(Time.time * 1.32f + .7f) * 3.1f, 0f);
        if (leftEye != null) leftEye.localRotation = leftRest * gaze;
        if (rightEye != null) rightEye.localRotation = rightRest * gaze;
        dissolve.SetFloat("_Gait", gaitBlend);
        foreach (var renderer in skinParts)
            if (renderer.enabled)
                renderer.SetPropertyBlock(dissolve);
    }

    private void Blink()
    {
        if (face == null)
            return;
        if (Time.time >= nextBlink)
        {
            blinkBegin = Time.time;
            nextBlink = Time.time + 2.65f + Mathf.Sin(Time.time * 1.7f) * .68f;
        }
        float phase = Mathf.Clamp01((Time.time - blinkBegin) / .19f);
        face.SetBlendShapeWeight(blinkIndex, Mathf.Sin(phase * Mathf.PI) * 100f);
    }

    internal void SetHuman(bool human)
    {
        foreach (var renderer in skinParts)
        {
            string part = renderer.name;
            if (part.StartsWith("Human feet", StringComparison.Ordinal))
                renderer.enabled = human;
            else if (part.StartsWith("Donkey hoof", StringComparison.Ordinal) || part.StartsWith("Furred equine", StringComparison.Ordinal))
                renderer.enabled = !human;
        }
    }

    internal void Dissolve(float progress, float floor)
    {
        dissolve.SetVector("_Vanish", new Vector4(progress, floor, 0f, 0f));
        foreach (var renderer in skinParts)
            if (renderer.enabled)
                renderer.SetPropertyBlock(dissolve);
    }

    private static GroundedHoof FindHoof(Transform modelRoot, string side)
    {
        Transform hip = null, knee = null, ankle = null;
        foreach (var joint in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (joint.name == "upperleg02" + side) hip = joint;
            if (joint.name == "lowerleg01" + side) knee = joint;
            if (joint.name == "foot" + side) ankle = joint;
        }
        return hip != null && knee != null && ankle != null ? new GroundedHoof(hip, knee, ankle) : null;
    }
    private sealed class GroundedHoof
    {
        private readonly Transform hip, knee, ankle;
        private Vector3 planted;
        private Quaternion plantedRotation;
        private bool locked;

        internal GroundedHoof(Transform hip, Transform knee, Transform ankle)
        {
            this.hip = hip;
            this.knee = knee;
            this.ankle = ankle;
        }

        internal void Update(float phase, float floor, bool walking)
        {
            if (!walking || phase < .065f || phase > .435f)
            {
                locked = false;
                return;
            }

            Vector3 current = ankle.position;
            if (!locked)
            {
                planted = new Vector3(current.x, floor + .074f, current.z);
                plantedRotation = ankle.rotation;
                locked = true;
            }

            Vector3 root = hip.position;
            Vector3 joint = knee.position;
            float thigh = Vector3.Distance(root, joint);
            float shin = Vector3.Distance(joint, current);
            Vector3 line = planted - root;
            float distance = line.magnitude;
            if (distance < .01f || distance > thigh + shin + .10f)
                return;

            float weight = Mathf.SmoothStep(0, 1, Mathf.Min((phase - .065f) / .095f, (.435f - phase) / .095f));
            Vector3 aim = Vector3.Lerp(current, planted, weight);
            Vector3 axis = (aim - root).normalized;
            distance = Mathf.Clamp(Vector3.Distance(root, aim), Mathf.Abs(thigh - shin) + .001f, thigh + shin - .001f);
            Vector3 bend = Vector3.ProjectOnPlane(joint - root, axis).normalized;
            if (bend.sqrMagnitude < .001f)
                return;

            float along = (thigh * thigh - shin * shin + distance * distance) / (2f * distance);
            float across = Mathf.Sqrt(Mathf.Max(0f, thigh * thigh - along * along));
            Vector3 solvedKnee = root + axis * along + bend * across;
            Quaternion before = ankle.rotation;
            hip.rotation = Quaternion.FromToRotation(joint - root, solvedKnee - root) * hip.rotation;
            knee.rotation = Quaternion.FromToRotation(ankle.position - knee.position,
                root + axis * distance - knee.position) * knee.rotation;
            ankle.rotation = Quaternion.Slerp(before, plantedRotation, weight);
        }
    }
    private void Dress(GameObject model)
    {
        Shader shader = Resources.Load<Shader>("Hamaret/HamaretCharacterLighting");
        Texture skin = Resources.Load<Texture>("Hamaret/HamaretSkin");
        Texture eyes = Resources.Load<Texture>("Hamaret/HamaretEyes");
        if (shader == null || !shader.isSupported)
            return;

        var remapped = new Dictionary<Material, Material>();
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            for (int index = 0; index < slots.Length; index++)
            {
                Material original = slots[index];
                if (original == null)
                    continue;
                if (!remapped.TryGetValue(original, out Material material))
                {
                    material = new Material(shader) { name = original.name + " (Hamaret)" };
                    materials.Add(material);
                    string part = renderer.name.ToLowerInvariant();
                    Color color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.color;
                    Texture texture = original.mainTexture;
                    float weave = 0f;
                    float gloss = 0.035f;
                    if (fitted && original.name.Contains("GrayTexture_Eyes"))
                    {
                        color = Color.white;
                        material.SetFloat("_Eye", 1f);
                        gloss = .060f;
                    }
                    else if (fitted && original.name.Contains("GrayTexture_Head"))
                    {
                        color = Color.white;
                        texture = Resources.Load<Texture>("Hamaret/MetaHuman/Face_Basecolor");
                        material.SetFloat("_SkinDetail", 1f);
                        gloss = .024f;
                    }
                    else if (fitted && original.name.Contains("GrayTexture_Body"))
                    {
                        color = new Color(.72f, .49f, .37f);
                        texture = Resources.Load<Texture>("Hamaret/MetaHuman/Body_Basecolor");
                        material.SetFloat("_SkinDetail", 1f);
                        gloss = .021f;
                    }
                    else if (part.Contains("browstrands"))
                    {
                        color = new Color(.19f, .13f, .10f);
                        gloss = 0f;
                    }
                    else if (part.Contains("hamaretmakehumanbase") || part.StartsWith("human feet", StringComparison.Ordinal))
                    {
                        color = new Color(0.78f, 0.82f, 0.84f);
                        texture = skin != null ? skin : texture;
                        material.SetFloat("_SkinDetail", 1f);
                        gloss = 0.021f;
                    }
                    else if (part.Contains("high-poly"))
                    {
                        color = Color.white;
                        texture = eyes != null ? eyes : texture;
                        material.SetFloat("_MaskAlpha", 1f);
                        gloss = 0.028f;
                    }
                    else if (part.Contains("abaya") || part.Contains("drape"))
                    {
                        color = new Color(0.035f, 0.030f, 0.029f);
                        weave = fitted ? .85f : 1f;
                        gloss = 0.004f;
                    }
                    else if (part.Contains("equine") || original.name.Contains("fetlock"))
                    {
                        color = new Color(0.17f, 0.096f, 0.06f);
                        weave = 0.7f;
                    }
                    else if (part.Contains("hoof"))
                    {
                        color = new Color(0.063f, 0.043f, 0.034f);
                    }
                    material.SetColor("_BaseColor", color);
                    if (texture != null)
                        material.SetTexture("_BaseMap", texture);
                    material.SetFloat("_Weave", weave);
                    material.SetFloat("_Gloss", gloss);
                    remapped.Add(original, material);
                }
                slots[index] = material;
            }
            renderer.sharedMaterials = slots;
        }
    }
    public void Dispose()
    {
        if (graph.IsValid())
            graph.Destroy();
        foreach (var material in materials)
            Object.Destroy(material);
    }
}
