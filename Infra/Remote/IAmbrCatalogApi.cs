namespace Practical1.Infra.Remote;

public interface IAmbrCatalogApi
{
    Task<IReadOnlyList<AmbrSummary>> GetCharacterSummariesAsync();
    Task<IReadOnlyList<AmbrSummary>> GetWeaponSummariesAsync();
}
