using UnityEngine;

public sealed class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Global")]
    [SerializeField] private float masterVolume = 1f;

    [Header("Mining")]
    [SerializeField] private AudioClip pickaxeMineClip;
    [SerializeField] private AudioClip drillMineClip;

    [Header("Item")]
    [SerializeField] private AudioClip itemPickClip;
    [SerializeField] private AudioClip itemPlaceClip;

    [Header("Factory")]
    [SerializeField] private AudioClip pressClip;

    [Header("Cash")]
    [SerializeField] private AudioClip cashSpawnClip;
    [SerializeField] private AudioClip cashCollectClip;

    [Header("Unlock")]
    [SerializeField] private AudioClip unlockFillClip;
    [SerializeField] private AudioClip unlockCompleteClip;

    [Header("UI")]
    [SerializeField] private AudioClip endingShowClip;

    [Header("Cooldown")]
    [SerializeField] private float pickaxeCooldown = 0.04f;
    [SerializeField] private float drillCooldown = 0.03f;
    [SerializeField] private float unlockFillCooldown = 0.04f;
    [SerializeField] private float cashSpawnCooldown = 0.12f;

    private float lastPickaxeTime = -999f;
    private float lastDrillTime = -999f;
    private float lastUnlockFillTime = -999f;
    private float lastCashSpawnTime = -999f;
    private AudioSource uiAudioSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        uiAudioSource = GetComponent<AudioSource>();
        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
        }

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
    }

    public void PlayPickaxeMine(Vector3 position)
    {
        if (!PassCooldown(ref lastPickaxeTime, pickaxeCooldown)) return;
        PlayAtPosition(pickaxeMineClip, position);
    }

    public void PlayDrillMine(Vector3 position)
    {
        if (!PassCooldown(ref lastDrillTime, drillCooldown)) return;
        PlayAtPosition(drillMineClip, position);
    }

    public void PlayItemPick(Vector3 position)
    {
        PlayAtPosition(itemPickClip, position);
    }

    public void PlayItemPlace(Vector3 position)
    {
        PlayAtPosition(itemPlaceClip, position);
    }

    public void PlayPress(Vector3 position)
    {
        PlayAtPosition(pressClip, position);
    }

    public void PlayCashSpawn(Vector3 position)
    {
        if (!PassCooldown(ref lastCashSpawnTime, cashSpawnCooldown)) return;
        PlayAtPosition(cashSpawnClip, position);
    }

    public void PlayCashCollect(Vector3 position)
    {
        PlayAtPosition(cashCollectClip, position);
    }

    public void PlayUnlockFill(Vector3 position)
    {
        if (!PassCooldown(ref lastUnlockFillTime, unlockFillCooldown)) return;
        PlayAtPosition(unlockFillClip, position);
    }

    public void PlayUnlockComplete(Vector3 position)
    {
        PlayAtPosition(unlockCompleteClip, position);
    }

    public void PlayEndingShow()
    {
        Play2D(endingShowClip);
    }

    private bool PassCooldown(ref float lastTime, float cooldown)
    {
        if (Time.time - lastTime < Mathf.Max(0f, cooldown)) return false;
        lastTime = Time.time;
        return true;
    }

    private void Play2D(AudioClip clip)
    {
        if (clip == null) return;
        if (masterVolume <= 0f) return;
        if (uiAudioSource == null) return;
        uiAudioSource.PlayOneShot(clip, masterVolume);
    }

    private void PlayAtPosition(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;
        if (masterVolume <= 0f) return;
        AudioSource.PlayClipAtPoint(clip, position, masterVolume);
    }
}
