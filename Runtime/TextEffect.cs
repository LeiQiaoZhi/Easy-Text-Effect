using System.Collections.Generic;
using System.Linq;
using EasyTextEffects.Editor.EditorDocumentation;
using EasyTextEffects.Effects;
using MyBox;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace EasyTextEffects
{
    [ExecuteAlways]
    public class TextEffect : MonoBehaviour
    {
        public TMP_Text text;
        [Space(5)] public bool usePreset;

        [ConditionalField(nameof(usePreset), false)]
        public TagEffectsPreset preset;

        public List<TextEffectEntry> tagEffects;

        [Space(5)] [FormerlySerializedAs("effectsList")]
        public List<GlobalTextEffectEntry> globalEffects;

        [Space(5)] [Range(1, 120)] public int updatesPerSecond = 30;

        private List<TextEffectEntry> allTagEffects_;
        private List<TextEffectEntry> onStartTagEffects_;
        private List<TextEffectEntry> manualTagEffects_;
        private List<GlobalTextEffectEntry> onStartEffects_;
        private List<GlobalTextEffectEntry> manualEffects_;
        private List<TextEffect_Trigger> entryEffectsCopied_;

        public void UpdateStyleInfos()
        {
            if (text == null || text.textInfo == null)
                return;
            TMP_TextInfo textInfo = text.textInfo;

            var styles = textInfo.linkInfo;

            // copy global effects
            onStartEffects_ = new List<GlobalTextEffectEntry>();
            manualEffects_ = new List<GlobalTextEffectEntry>();
            globalEffects.ForEach(_entry =>
            {
                if (_entry.effect == null)
                    return;
                var effectEntry = new GlobalTextEffectEntry();
                effectEntry.effect = _entry.effect.Instantiate();
                effectEntry.effect.startCharIndex = 0;
                effectEntry.effect.charLength = textInfo.characterCount;
                effectEntry.overrideTagEffects = _entry.overrideTagEffects;
                if (_entry.triggerWhen == GlobalTextEffectEntry.TriggerWhen.OnStart)
                    onStartEffects_.Add(effectEntry);
                else
                    manualEffects_.Add(effectEntry);
            });

            // add effects to list
            allTagEffects_ = new List<TextEffectEntry>(tagEffects);
            if (usePreset && preset != null)
                allTagEffects_.AddRange(preset.tagEffects);
            
            onStartTagEffects_ = new List<TextEffectEntry>();
            manualTagEffects_ = new List<TextEffectEntry>();
            for (var i = 0; i < styles.Length; i++)
            {
                TMP_LinkInfo style = styles[i];
                if (style.GetLinkID() == string.Empty)
                    continue;

                // copy effects
                var effectTemplates = GetTagEffectsByName(style.GetLinkID());
                foreach (TextEffectEntry entry in effectTemplates)
                {
                    if (entry.effect == null)
                        continue;
                    var entryCopy = new TextEffectEntry();
                    entryCopy.effect = entry.effect.Instantiate();
                    entryCopy.effect.startCharIndex = style.linkTextfirstCharacterIndex;
                    entryCopy.effect.charLength = style.linkTextLength;
                    if (entry.triggerWhen == TextEffectEntry.TriggerWhen.OnStart)
                        onStartTagEffects_.Add(entryCopy);
                    else
                        manualTagEffects_.Add(entryCopy);
                }
            }

            StartOnStartEffects();
        }

        private List<TextEffectEntry> GetTagEffectsByName(string _effectName)
        {
            var results = new List<TextEffectEntry>();
            var effectNames = _effectName.Split("+");

            foreach (var effectName in effectNames)
            {
                var findAll = allTagEffects_.FindAll(_entry => _entry.effect?.effectTag == effectName);
                if (findAll.Count >= 1)
                    results.Add(findAll[0]);
#if UNITY_EDITOR
                if (findAll.Count == 0)
                    Debug.LogWarning("Effect not found: " + effectName);

                if (findAll.Count > 1)
                {
                    Debug.LogWarning("Multiple effects found: " + effectName);
                    Debug.Log("Effects: " + string.Join(", ", findAll.Select(_entry => _entry.effect.name).ToArray()));
                }
#endif
            }

            return results;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (text == null)
                return;
            text.ForceMeshUpdate();
            UpdateStyleInfos();
#endif
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update += Update;
#endif
            if (text == null)
                return;
            Debug.Log("TextEffect enabled");
            text.ForceMeshUpdate();
            UpdateStyleInfos();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= Update;
#endif
        }

        private void Start()
        {
            if (text == null)
                return;
            // Debug.Log("TextEffect started");
            // text.ForceMeshUpdate();
            // UpdateStyleInfos();
        }

        private float nextUpdateTime_ = 0;

        public void Update()
        {
            if (!text)
                return;

            var time = TimeUtil.GetTime();
            if (time < nextUpdateTime_)
                return;
            nextUpdateTime_ = time + 1f / updatesPerSecond;

            text.ForceMeshUpdate();
            TMP_TextInfo textInfo = text.textInfo;

            for (var i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;

                var capturedI = i;
                onStartEffects_.Where(_entry => !_entry.overrideTagEffects)
                    .ForEach(_entry => _entry.effect.ApplyEffect(textInfo, capturedI, 0, 3));
                manualEffects_.Where(_entry => !_entry.overrideTagEffects).ForEach(_entry =>
                    _entry.effect.ApplyEffect(textInfo, capturedI, 0, 3));

                onStartTagEffects_.ForEach(_entry => _entry.effect.ApplyEffect(textInfo, capturedI, 0, 3));
                manualTagEffects_.ForEach(_entry => _entry.effect.ApplyEffect(textInfo, capturedI, 0, 3));

                onStartEffects_.Where(_entry => _entry.overrideTagEffects).ForEach(_entry =>
                    _entry.effect.ApplyEffect(textInfo, capturedI, 0, 3));
                manualEffects_.Where(_entry => _entry.overrideTagEffects).ForEach(_entry =>
                    _entry.effect.ApplyEffect(textInfo, capturedI, 0, 3));
            }

            // apply changes and update mesh
            for (var i = 0; i < textInfo.meshInfo.Length; i++)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[i];

                meshInfo.mesh.colors32 = meshInfo.colors32;
                meshInfo.mesh.vertices = meshInfo.vertices;

                text.UpdateGeometry(meshInfo.mesh, i);
            }
        }

        public void StartOnStartEffects()
        {
            onStartEffects_.ForEach(_effect => _effect.effect.StartEffect());
            onStartTagEffects_.ForEach(_effect => _effect.effect.StartEffect());
            nextUpdateTime_ = 0; // immediately update
        }

        public void StartManualEffects()
        {
            manualEffects_.ForEach(_effect => _effect.effect.StartEffect());
        }

        public void StopManualEffects()
        {
            manualEffects_.ForEach(_effect => _effect.effect.StopEffect());
        }

        public void StartManualTagEffects()
        {
            manualTagEffects_.ForEach(_effect => _effect.effect.StartEffect());
        }

        public void StopManualTagEffects()
        {
            manualTagEffects_.ForEach(_effect => _effect.effect.StopEffect());
        }

        public void StartManualEffect(string _effectName)
        {
            GlobalTextEffectEntry effectEntry = manualEffects_.Find(_entry => _entry.effect.effectTag == _effectName);

            var names = manualEffects_.Select(_entry => _entry.effect.effectTag).ToList();

            if (effectEntry != null)
                effectEntry.effect.StartEffect();
            else
            {
                Debug.LogWarning($"Effect {_effectName} not found");
                Debug.Log($"Available effects: {string.Join(", ", names)}");
            }
        }

        public void StartManualTagEffect(string _effectName)
        {
            TextEffectEntry effectEntry = manualTagEffects_.Find(_entry => _entry.effect.effectTag == _effectName);

            var names = manualTagEffects_.Select(_entry => _entry.effect.effectTag).ToList();

            if (effectEntry != null)
                effectEntry.effect.StartEffect();
            else
            {
                Debug.LogWarning($"Effect {_effectName} not found");
                Debug.Log($"Available effects: {string.Join(", ", names)}");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (text == null || text.textInfo == null)
                return;
            Gizmos.color = Color.green;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = text.textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;

                Vector3 botLeft = text.transform.TransformPoint(charInfo.bottomLeft);
                Vector3 topRight = text.transform.TransformPoint(charInfo.topRight);

                var verts = text.textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
                var first = verts[charInfo.vertexIndex + 0];
                var last = verts[charInfo.vertexIndex + 2];

                first = text.transform.TransformPoint(first);
                last = text.transform.TransformPoint(last);

                Gizmos.color = Color.red;
                // Gizmos.DrawWireSphere(first, 10);
                // Gizmos.DrawWireSphere(last,     10);

                // Gizmos.DrawWireCube((botLeft + topRight) / 2, topRight - botLeft);
            }
        }
#endif
    }
}