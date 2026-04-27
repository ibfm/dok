namespace Dok.Domain.Tests;

public class DebtTypeMapperTests
{
    [Theory]
    [InlineData("IPVA", DebtType.Ipva)]
    [InlineData("ipva", DebtType.Ipva)]
    [InlineData(" MULTA ", DebtType.Multa)]
    [InlineData("multa", DebtType.Multa)]
    public void Parse_with_known_returns_enum(string raw, DebtType expected)
    {
        DebtTypeMapper.Parse(raw).ShouldBe(expected);
    }

    [Theory]
    [InlineData("LICENCIAMENTO")]
    [InlineData("DPVAT")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_with_unknown_throws(string? raw)
    {
        var ex = Should.Throw<UnknownDebtTypeException>(() => DebtTypeMapper.Parse(raw));
        ex.Type.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(DebtType.Ipva, "IPVA")]
    [InlineData(DebtType.Multa, "MULTA")]
    public void ToWire_returns_uppercase(DebtType type, string expected)
    {
        DebtTypeMapper.ToWire(type).ShouldBe(expected);
    }
}
