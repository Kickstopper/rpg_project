using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using global::UI.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RPGProject.Editor.VFX
{
    /// <summary>Only this draft is edited/recorded in Undo. Playback never touches the prefab.</summary>
    public sealed class BattleVFXDocument : ScriptableObject
    {
        public BattleVFXAnimator.FrameData[] frames = Array.Empty<BattleVFXAnimator.FrameData>();
        public float defaultDuration = 0.1f;
        public bool useNativeSize = true;
        [HideInInspector] public string sourceGuid;
        [HideInInspector] public string sourceHash;
        [HideInInspector] public string savedContent;
        public string AssetPath => AssetDatabase.GUIDToAssetPath(sourceGuid);
        public bool HasChanges => Content() != savedContent;

        [Serializable]
        private sealed class ContentState
        {
            public BattleVFXAnimator.FrameData[] frames;
            public float defaultDuration;
            public bool useNativeSize;
        }

        private string Content() => JsonUtility.ToJson(new ContentState
        { frames = frames, defaultDuration = defaultDuration, useNativeSize = useNativeSize });

        public void Load(GameObject prefab)
        {
            var animator = FindAnimator(prefab);
            string path = AssetDatabase.GetAssetPath(prefab);
            string hash = FileHash(path);
            frames = animator.frames == null ? Array.Empty<BattleVFXAnimator.FrameData>() : (BattleVFXAnimator.FrameData[])animator.frames.Clone();
            defaultDuration = animator.defaultDuration;
            useNativeSize = animator.useNativeSize;
            sourceGuid = AssetDatabase.AssetPathToGUID(path);
            sourceHash = hash;
            savedContent = Content();
        }

        public static BattleVFXAnimator FindAnimator(GameObject prefab)
        {
            if (prefab == null) throw new InvalidOperationException("프리팹을 선택해 주세요.");
            var animators = prefab.GetComponentsInChildren<BattleVFXAnimator>(true);
            if (animators.Length != 1)
                throw new InvalidOperationException("BattleVFXAnimator가 하나인 프리팹을 선택해 주세요. 현재: " + animators.Length);
            if (animators[0].GetComponent<Image>() == null)
                throw new InvalidOperationException("애니메이터와 같은 오브젝트에 Image가 필요합니다.");
            return animators[0];
        }

        public string Validate()
        {
            if (!BattleVFXTimeline.IsPositiveFinite(defaultDuration)) return "기본 시간은 0보다 큰 유한한 초 단위 값이어야 합니다.";
            if (frames == null || frames.Length == 0) return "프레임을 하나 이상 추가해 주세요.";
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i].frameSprite == null) return (i + 1) + "번 프레임의 스프라이트가 없습니다.";
                if (!BattleVFXTimeline.IsPositiveFinite(frames[i].duration)) return (i + 1) + "번 프레임의 시간이 올바르지 않습니다.";
            }
            return null;
        }

        public float[] Durations() => frames == null ? Array.Empty<float>() : frames.Select(f => f.duration).ToArray();

        public void Append(Sprite[] sprites)
        {
            if (sprites == null || sprites.Length == 0) return;
            frames = (frames ?? Array.Empty<BattleVFXAnimator.FrameData>()).Concat(sprites.Where(s => s != null)
                .Select(s => new BattleVFXAnimator.FrameData { frameSprite = s, duration = defaultDuration })).ToArray();
        }

        public void ApplyDefaultToAll()
        {
            if (frames == null) return;
            for (int i = 0; i < frames.Length; i++) frames[i].duration = defaultDuration;
        }

        public void Save()
        {
            string problem = Validate();
            if (problem != null) throw new InvalidOperationException(problem);
            string path = AssetPath;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("원본 프리팹을 찾을 수 없습니다.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이 모드를 종료한 뒤 저장해 주세요.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
                throw new InvalidOperationException("같은 프리팹이 Prefab Mode에 열려 있습니다. 해당 편집을 저장하고 닫은 뒤 다시 시도해 주세요.");
            if (sourceHash != FileHash(path))
                throw new InvalidOperationException("원본 프리팹이 다른 곳에서 변경되었습니다. 현재 초안을 복사해 두거나 원본 다시 읽기로 최신 내용을 확인해 주세요.");
            if (!AssetDatabase.IsOpenForEdit(path))
                throw new InvalidOperationException("프리팹이 읽기 전용입니다. 버전 관리의 체크아웃/쓰기 권한을 확인해 주세요.");

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                var animator = FindAnimator(root);
                // Preserve images, Image settings, hierarchy, other components and prefab metadata.
                animator.generateFramesNow = false;
                animator.frames = (BattleVFXAnimator.FrameData[])frames.Clone();
                animator.defaultDuration = defaultDuration;
                animator.useNativeSize = useNativeSize;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success) throw new IOException("프리팹 저장에 실패했습니다. 초안은 유지됩니다.");
            }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            sourceHash = FileHash(path);
            savedContent = Content();
        }

        private static string FileHash(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("프리팹 파일을 찾을 수 없습니다.", path);
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
