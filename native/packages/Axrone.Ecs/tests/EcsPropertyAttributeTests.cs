namespace Axrone.Ecs.Tests;

internal struct EcsInspectedComponent
{
    [Property(Label = "Volume", Type = PropertyTypes.Number, Min = 0.0, Max = 1.0, Step = 0.01)]
    public float Volume;

    [Property(Label = "Name", Type = PropertyTypes.Text, Visible = false)]
    public string Name;

    public int Unmarked;

    public EcsInspectedComponent(float volume, string name, int unmarked = 0)
    {
        Volume = volume;
        Name = name;
        Unmarked = unmarked;
    }
}

public class EcsPropertyAttributeTests
{
    private static PropertyAttribute? Read(string field)
    {
        object[] attributes = typeof(EcsInspectedComponent).GetField(field)!
            .GetCustomAttributes(typeof(PropertyAttribute), false);
        return attributes.Length > 0 ? (PropertyAttribute)attributes[0] : null;
    }

    [Fact]
    public void Attribute_Carries_Editor_Metadata()
    {
        PropertyAttribute? volume = Read(nameof(EcsInspectedComponent.Volume));
        volume.Should().NotBeNull();
        volume!.Label.Should().Be("Volume");
        volume.Type.Should().Be(PropertyTypes.Number);
        volume.Min.Should().Be(0.0);
        volume.Max.Should().Be(1.0);
        volume.Step.Should().Be(0.01);
        volume.Serializable.Should().BeTrue();
        volume.Visible.Should().BeTrue();
    }

    [Fact]
    public void Attribute_Defaults_Serializable_Visible()
    {
        PropertyAttribute? name = Read(nameof(EcsInspectedComponent.Name));
        name.Should().NotBeNull();
        name!.Visible.Should().BeFalse();
        name.Serializable.Should().BeTrue();
        name.Min.Should().Be(double.NaN);
    }

    [Fact]
    public void Unmarked_Field_Has_NoAttribute()
    {
        Read(nameof(EcsInspectedComponent.Unmarked)).Should().BeNull();
    }
}
