using ClinicManagementApp.Api.Common;
using FluentAssertions;

namespace ClinicManagementApp.Api.Tests;

public class SyncLogRedactorTests
{
    [Fact]
    public void MaskFreeText_MasksRunsOf7OrMoreDigits()
    {
        var input = "Call patient at 0917-123-4567 or room 101.";
        var result = SyncLogRedactor.MaskFreeText(input);

        result.Should().Be("Call patient at *** or room 101.");
    }

    [Fact]
    public void RedactAndTruncate_MasksSensitiveKeys_AndPreservesIdsAndAmounts()
    {
        var json = """
        {
            "id": 42,
            "patientName": "Juan Dela Cruz",
            "phone": "09179998888",
            "amount": 1500.50,
            "status": "Completed"
        }
        """;

        var result = SyncLogRedactor.RedactAndTruncate(json);

        result.Should().Contain("\"id\":42");
        result.Should().Contain("\"amount\":1500.5");
        result.Should().Contain("\"status\":\"Completed\"");
        result.Should().Contain("\"patientName\":\"***\"");
        result.Should().Contain("\"phone\":\"***\"");
        result.Should().NotContain("Juan Dela Cruz");
        result.Should().NotContain("09179998888");
    }

    [Fact]
    public void RedactAndTruncate_MasksNestedPayloadJson()
    {
        var json = """
        {
            "entityType": "patient",
            "entityId": "100",
            "payloadJson": "{\"firstName\":\"Maria\",\"lastName\":\"Clara\",\"phone\":\"09181112222\",\"notes\":\"Confidential medical note\"}"
        }
        """;

        var result = SyncLogRedactor.RedactAndTruncate(json);

        result.Should().Contain("\"entityType\":\"patient\"");
        result.Should().Contain("\"entityId\":\"100\"");
        result.Should().NotContain("Maria");
        result.Should().NotContain("Clara");
        result.Should().NotContain("09181112222");
        result.Should().NotContain("Confidential medical note");
    }

    [Fact]
    public void RedactAndTruncate_TruncatesWhenExceedingMax()
    {
        var longString = new string('x', 3000);
        var result = SyncLogRedactor.RedactAndTruncate(longString, max: 2048);

        result.Length.Should().BeGreaterThan(2048);
        result.Should().EndWith("chars]");
    }
}

