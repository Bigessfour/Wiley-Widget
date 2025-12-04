using System.Collections.Generic;
using WileyWidget.Models;
using WileyWidget.Abstractions.Models;

namespace WileyWidget.Business.Interfaces
{
    /// <summary>
    /// Maps domain MunicipalAccount instances to UI/display DTOs.
    /// Kept simple and free of external deps to allow easy unit testing.
    /// </summary>
    public interface IAccountMapper
    {
        IEnumerable<MunicipalAccountDisplay> MapToDisplay(IEnumerable<MunicipalAccount> domainAccounts);

        MunicipalAccountDisplay MapToDisplay(MunicipalAccount account);
    }
}