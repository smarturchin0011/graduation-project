using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instence;
    private AudioSource _audioSource;
    private Dictionary<string, AudioClip> audioDic;

    private void Awake()
    {
        Instence = this;
        _audioSource = GetComponent<AudioSource>();
        audioDic = new Dictionary<string, AudioClip>();
    }

    /// <summary>
    /// 加载音频
    /// </summary>
    /// <param name="path">传入对应音频路径</param>
    /// <returns></returns>
    public AudioClip LoadAudio (string path)
    {
        return (AudioClip)Resources.Load(path);
    }

    /// <summary>
    /// 获取音频内容（如果没有自动缓存）
    /// </summary>
    /// <param name="path">对应音频路径</param>
    /// <returns></returns>
    public AudioClip GetAudio(string path)
    {
        if (!audioDic.ContainsKey(path))
        {
            audioDic[path] = LoadAudio(path);
        }
        return audioDic[path];
    }

    /// <summary>
    /// 播放BGM
    /// </summary>
    /// <param name="name">音乐名称</param>
    /// <param name="volume">音量大小（默认为 1）</param>
    public void PlayBGM(string path,float volume = 1.0f)
    {
        _audioSource.Stop();
        _audioSource.clip = LoadAudio(path);
        _audioSource.volume = volume;
        _audioSource.Play();
    }

    /// <summary>
    /// 停止播放BGM
    /// </summary>
    public void StopBGM()
    {
        _audioSource.Stop();
    }

    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="path">音频路径</param>
    /// <param name="volume">音频音量</param>
    public void PlaySoundEffect(string path, float volume = 1.0f)
    {
        _audioSource.PlayOneShot(LoadAudio(path),volume);
    }

    /// <summary>
    /// 播放音效（带 AudioSource 重载）
    /// </summary>
    /// <param name="audioSource">对象AudioSource</param>
    /// <param name="path">音频路径</param>
    /// <param name="volume">音量</param>
    public void PlaySoundEffect(AudioSource audioSource, string path, float volume = 1.0f)
    {
        audioSource.PlayOneShot(LoadAudio(path),volume);
    }
    
}
