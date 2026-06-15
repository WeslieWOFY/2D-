using System.Collections;
using System.Collections.Generic;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

public class AudioManager : PersistentSingleton<AudioManager>
{
    [SerializeField] AudioSource sFxPlayer;
    [SerializeField] AudioSource xuli;
    [SerializeField] protected AudioClip xuliSFX;
    protected override void Start()
    {
        xuli.clip=xuliSFX;
    }
    public void PlaySFX(AudioClip audioClip,float volume)
    {
        sFxPlayer.PlayOneShot(audioClip,volume);
    }
    public void PlayxuliSFX(float volume)
    {    
        xuli.volume = volume;
        xuli.loop=true;
        xuli.Play();
    }
    public void StopxuliSFX()
    {
        xuli.Stop();
        xuli.loop=false;
    }
}
