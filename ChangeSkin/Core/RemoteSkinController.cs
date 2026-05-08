using UnityEngine;

namespace ChangeSkinMP;

public class RemoteSkinController : MonoBehaviour
{
    private ChangeBody _body;
    public bool Banned { get; private set; }

    private void Awake()
    {
        _body = GetComponent<ChangeBody>();
    }

    public void SetSkin(SkinObject skin)
    {
        _body.ApplySkin(skin);
    }

    public void Enable()
    {
        if (Banned)
            return;
        _body.RepStart();
    }

    public void Disable()
    {
        _body.RepEnd();
    }

    public void OnBanReceived(bool banned)
    {
        Banned = banned;
        _body.RepEnd();
    }
}
