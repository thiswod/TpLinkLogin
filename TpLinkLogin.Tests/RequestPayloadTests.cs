using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

public class RequestPayloadTests
{
    [Fact]
    public void BuildGetEncryptInfoRequestBody_UsesExpectedKeysAndNullValue()
    {
        var body = TplinkLoginHelper.BuildGetEncryptInfoRequestBody();
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(body, options);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("method", out var method));
        Assert.Equal("do", method.GetString());

        Assert.True(doc.RootElement.TryGetProperty("user_management", out var userManagement));
        Assert.True(userManagement.TryGetProperty("get_encrypt_info", out var getEncryptInfo));
        Assert.Equal(JsonValueKind.Null, getEncryptInfo.ValueKind);
    }
}

