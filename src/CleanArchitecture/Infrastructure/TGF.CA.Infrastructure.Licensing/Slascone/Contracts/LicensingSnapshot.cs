using Slascone.Client;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

/// <summary>License information and compliance evaluated from the same published licensing state.</summary>
public sealed record LicensingSnapshot(LicenseInfoDto? LicenseInfo, LicenseComplianceStatus ComplianceStatus);
