namespace BlazorFridaApp.Components.Base;

public class SelectItem<T>
{
    public T? Value { get; set; }
    public string Text { get; set; } = string.Empty;

    public SelectItem() { }

    public SelectItem(T value, string text)
    {
        Value = value;
        Text = text;
    }
}