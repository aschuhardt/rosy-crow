namespace RosyCrow.Controls;

internal class TemplateContainer<T> : ContentView
{
    public static readonly BindableProperty ChildProperty = BindableProperty.Create(nameof(Child), typeof(T), typeof(TemplateContainer<T>));

    public static readonly BindableProperty ChildNameProperty =
        BindableProperty.Create(nameof(ChildName), typeof(string), typeof(TemplateContainer<T>));

    public T Child
    {
        get => (T)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    public string ChildName
    {
        get => (string)GetValue(ChildNameProperty);
        set => SetValue(ChildNameProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Child = (T)GetTemplateChild(ChildName);
    }
}