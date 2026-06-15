using ClipVaultApp.Services;

namespace ClipVault.App.Tests;

public class HolderTypesTests
{
    [Fact]
    public void PassphraseProvider_exposes_the_supplied_passphrase()
    {
        Assert.Equal("secret", new PassphraseProvider("secret").Passphrase);
    }

    [Fact]
    public void PassphraseProvider_allows_null()
    {
        Assert.Null(new PassphraseProvider(null).Passphrase);
    }

    [Fact]
    public void ResolvedMasterKey_starts_null_and_is_settable()
    {
        var holder = new ResolvedMasterKey();
        Assert.Null(holder.Dek);

        holder.Dek = [1, 2, 3];

        Assert.Equal(new byte[] { 1, 2, 3 }, holder.Dek);
    }
}
