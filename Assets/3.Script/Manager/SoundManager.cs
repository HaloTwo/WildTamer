using System;
using System.Collections.Generic;
using UnityEngine;

public enum BGMType
{
    Field,
}

public enum SFXType
{
    PlayerAttack,
    PlayerHit,
    GetCoin,
    Recruit,
    Dead
}

public class SoundManager : Singleton<SoundManager>
{
    [Header("Ref")]
    [SerializeField] AudioSource bgmSource;

    [Header("SFX Pool")]
    [SerializeField] int sfxPoolSize = 10;

    readonly string bgmResourcePath = "Sound/BGM";
    readonly string sfxResourcePath = "Sound/SFX";
    readonly string footstepResourcePath = "Sound/Footstep";

    readonly Dictionary<BGMType, AudioClip> bgmMap = new();
    readonly Dictionary<SFXType, AudioClip> sfxMap = new();
    readonly List<AudioClip> footstepClips = new();

    AudioSource[] sfxSources;
    int sfxIndex;

    Transform sfxPoolRoot;

    protected override void Awake()
    {
        base.Awake();

        InitAudioSources();
        LoadAllBGM();
        LoadAllSFX();
        LoadAllFootsteps();
    }

    void InitAudioSources()
    {
        if (bgmSource == null)
            bgmSource = gameObject.AddComponent<AudioSource>();

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.volume = 0.5f;
        bgmSource.spatialBlend = 0f;

        GameObject poolRootObj = new GameObject("SFXPoolRoot");
        poolRootObj.transform.SetParent(transform);
        poolRootObj.transform.localPosition = Vector3.zero;
        sfxPoolRoot = poolRootObj.transform;

        sfxSources = new AudioSource[Mathf.Max(1, sfxPoolSize)];

        for (int i = 0; i < sfxSources.Length; i++)
        {
            GameObject child = new GameObject($"SFXSource_{i}");
            child.transform.SetParent(sfxPoolRoot);
            child.transform.localPosition = Vector3.zero;

            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 1f;
            source.spatialBlend = 0f;

            sfxSources[i] = source;
        }
    }

    void LoadAllBGM()
    {
        bgmMap.Clear();

        AudioClip[] clips = Resources.LoadAll<AudioClip>(bgmResourcePath);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null) continue;

            string enumName = RemovePrefix(clip.name, "BGM_");

            if (Enum.TryParse(enumName, out BGMType type))
            {
                if (!bgmMap.ContainsKey(type))
                    bgmMap.Add(type, clip);
                else
                    Debug.LogWarning($"[SoundManager] 중복된 BGM enum 매핑: {type}");
            }
            else
            {
                Debug.LogWarning($"[SoundManager] BGM enum 매핑 실패: {clip.name}");
            }
        }
    }

    void LoadAllSFX()
    {
        sfxMap.Clear();

        AudioClip[] clips = Resources.LoadAll<AudioClip>(sfxResourcePath);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null) continue;

            string enumName = RemovePrefix(clip.name, "SFX_");

            if (Enum.TryParse(enumName, out SFXType type))
            {
                if (!sfxMap.ContainsKey(type))
                    sfxMap.Add(type, clip);
                else
                    Debug.LogWarning($"[SoundManager] 중복된 SFX enum 매핑: {type}");
            }
            else
            {
                Debug.LogWarning($"[SoundManager] SFX enum 매핑 실패: {clip.name}");
            }
        }
    }

    void LoadAllFootsteps()
    {
        footstepClips.Clear();

        AudioClip[] clips = Resources.LoadAll<AudioClip>(footstepResourcePath);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null) continue;

            footstepClips.Add(clip);
        }

        if (footstepClips.Count == 0)
        {
            Debug.LogWarning("[SoundManager] Footstep 클립이 없음: Sound/Footstep");
        }
    }

    string RemovePrefix(string source, string prefix)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;
        if (string.IsNullOrEmpty(prefix)) return source;

        return source.StartsWith(prefix, StringComparison.Ordinal)
            ? source.Substring(prefix.Length)
            : source;
    }

    AudioSource GetNextSFXSource()
    {
        AudioSource source = sfxSources[sfxIndex];
        sfxIndex = (sfxIndex + 1) % sfxSources.Length;
        return source;
    }

    public void PlayBGM(BGMType type)
    {
        if (!bgmMap.TryGetValue(type, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning($"[SoundManager] BGM 없음: {type}");
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
            bgmSource.Pause();
    }

    public void ResumeBGM()
    {
        if (bgmSource.clip != null)
            bgmSource.UnPause();
    }

    public void PlaySFX(SFXType type)
    {
        if (!sfxMap.TryGetValue(type, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning($"[SoundManager] SFX 없음: {type}");
            return;
        }

        GetNextSFXSource().PlayOneShot(clip);
    }

    public void PlayFootstep()
    {
        if (footstepClips.Count == 0) return;

        AudioClip clip = footstepClips[UnityEngine.Random.Range(0, footstepClips.Count)];
        GetNextSFXSource().PlayOneShot(clip, 0.5f);
    }
}