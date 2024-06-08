using Menu;
using System;
using System.Linq;
using UnityEngine;

public class Jukebox : ExpeditionJukebox
{
    public bool opening;
    public bool closing;

    public float lastAlpha;
    public float currentAlpha;

    public float targetAlpha;
    public float uAlpha;

    public Jukebox(ProcessManager manager) : base(manager)
    {
        //this.pages[0].Container.AddChild(this.darkSprite);
        base.scene.UnloadImages();
        base.scene = null;

        opening = true;
        targetAlpha = 1f;
    }

    public override void Update()
    {
        base.Update();
        Debug.Log("Selectables: " + pages[0].selectables.Count() + "\tSubObjects: " + pages[0].subObjects.Count());
        base.Update();
        lastAlpha = currentAlpha;
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, 0.2f);
        if (opening && pages[0].pos.y <= 0.01f)
        {
            opening = false;
        }
        if (closing && Math.Abs(currentAlpha - targetAlpha) < 0.09f)
        {
            manager.StopSideProcess(this);
            closing = false;
        }

        if (opening)
        {
            return;
        }
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        if (opening || closing)
        {
            uAlpha = Mathf.Pow(Mathf.Max(0f, Mathf.Lerp(lastAlpha, currentAlpha, timeStacker)), 1.5f);
            //darkSprite.alpha = uAlpha * 0.95f;
        }
        pages[0].pos.y = Mathf.Lerp(manager.rainWorld.options.ScreenSize.y + 100f, 0.01f, (uAlpha < 0.999f) ? uAlpha : 1f);
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (message == "BACK")
        {
            this.closing = true;
            this.targetAlpha = 0f;
            this.PlaySound(SoundID.MENU_Switch_Page_Out);
            return;
        }
        base.Singal(sender, message);
    }
}
