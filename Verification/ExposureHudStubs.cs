namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Behaviour
    { public UnityEngine.Color color=UnityEngine.Color.white; public UnityEngine.RectTransform rectTransform {get{return GetComponent<UnityEngine.RectTransform>();}} }
    public class Text : Graphic {public string text="";}
}
