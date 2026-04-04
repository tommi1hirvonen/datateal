using DuckHouse.Ui.Client.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DuckHouse.Ui.Client.Components;

public class SvgIcon : ComponentBase
{
    [Parameter, EditorRequired] public Svg? Icon { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddMarkupContent(0, Icon?.Text);
    }
}