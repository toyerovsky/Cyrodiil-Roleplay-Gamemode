using System.Numerics;
using CyrodiilRP.Common.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class GameLightsTests
{
    private const float Precision = 0.01f;

    [Fact]
    public void A_light_form_s_name_gives_its_colour_and_reach()
    {
        Assert.True(GameLights.TryRead("/Game/Forms/worldobjects/light/CandleFat01Yellow256.CandleFat01Yellow256", out var candle));
        Assert.Equal([255, 205, 140], [candle.Red, candle.Green, candle.Blue]);
        Assert.Equal(256f * BuildingGrid.CentimetresPerUnit, candle.Radius, Precision);
        Assert.Equal(32f, candle.Candelas, Precision);
        Assert.Equal(LightHang.Standing, candle.Hang);

        Assert.True(GameLights.TryRead("/Game/Forms/worldobjects/light/LampSconce01DarkRed256.LampSconce01DarkRed256", out var sconce));
        Assert.Equal([180, 50, 40], [sconce.Red, sconce.Green, sconce.Blue]);

        Assert.True(GameLights.TryRead("/Game/Forms/worldobjects/light/ICPHangingLight01Orange512Flicker.ICPHangingLight01Orange512Flicker", out var flicker));
        Assert.Equal(512f * BuildingGrid.CentimetresPerUnit, flicker.Radius, Precision);
        Assert.Equal([255, 160, 85], [flicker.Red, flicker.Green, flicker.Blue]);
        Assert.Equal(LightHang.Hanging, flicker.Hang);
    }

    [Fact]
    public void A_name_without_a_reach_or_a_colour_lights_yellow_as_far_as_usual()
    {
        Assert.True(GameLights.TryRead("/Game/Forms/worldobjects/light/LightsIronLampHangingLong01.LightsIronLampHangingLong01", out var lamp));
        Assert.Equal(256f * BuildingGrid.CentimetresPerUnit, lamp.Radius, Precision);
        Assert.Equal([255, 205, 140], [lamp.Red, lamp.Green, lamp.Blue]);
        Assert.Equal(LightHang.Hanging, lamp.Hang);
    }

    [Fact]
    public void Only_the_game_s_light_forms_light()
    {
        Assert.False(GameLights.TryRead("/Game/Forms/worldobjects/static/AnvilLamp380.AnvilLamp380", out _));
        Assert.False(GameLights.TryRead("/Game/Art/Environment/clutter/SM_candle01.SM_candle01", out _));
    }

    [Fact]
    public void A_standing_light_shines_near_its_top_and_a_hanging_one_from_its_middle_turned_with_the_piece()
    {
        var position = new Vector3(1000f, 2000f, 300f);
        var min = new Vector3(0f, -10f, 0f);
        var max = new Vector3(20f, 10f, 100f);
        var standing = new GameLight(255, 205, 140, 300f, 30f, LightHang.Standing);
        var hanging = standing with { Hang = LightHang.Hanging };

        var flame = GameLights.FlameOf(standing, position, 90f, min, max);
        Assert.Equal(1000f, flame.X, Precision);
        Assert.Equal(2010f, flame.Y, Precision);
        Assert.Equal(385f, flame.Z, Precision);

        Assert.Equal(350f, GameLights.FlameOf(hanging, position, 0f, min, max).Z, Precision);
    }
}
