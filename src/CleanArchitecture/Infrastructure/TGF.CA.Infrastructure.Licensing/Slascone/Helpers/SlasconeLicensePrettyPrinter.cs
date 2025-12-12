using Microsoft.Extensions.Logging;
using Slascone.Client;
using Slascone.Client.Xml;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Helpers;

internal interface ISlasconeLicensePrinter {
    IDictionary<Guid, string> PrintLicenseDetails(LicenseDto license);
    void PrintLicenseDetails(LicenseXmlDto licenseXml);
    IDictionary<Guid, string> PrintLicenseDetails(LicenseInfoDto licenseInfo);
}

internal class SlasconeLicensePrinter(ILogger<SlasconeLicensePrinter> logger) : ISlasconeLicensePrinter {

    /// <summary>
    /// Prints the details of a LicenseDto object to the log.
    /// Displays license information, customer details, features, limitations, and variables.
    /// </summary>
    /// <param name="license">The license object to print</param>
    public IDictionary<Guid, string> PrintLicenseDetails(LicenseDto license) {
        if (license == null) {
            logger.LogDebug("No license information available.");
            return new Dictionary<Guid, string>();
        }

        logger.LogDebug("License details (Created: {CreatedDate})", license.Created_date_utc?.ToString("yyyy-MM-dd") ?? "N/A");

        // License Information
        logger.LogDebug("License Information:");
        logger.LogDebug("-------------------");
        logger.LogDebug("License ID: {LicenseId}", license.Id);
        logger.LogDebug("License Name: {LicenseName}", license.Name ?? "");
        if (!string.IsNullOrEmpty(license.Description)) {
            logger.LogDebug("Description: {Description}", license.Description);
        }
        if (!string.IsNullOrEmpty(license.Legacy_license_key)) {
            logger.LogDebug("Legacy License Key: <secret>");
        }
        logger.LogDebug("Client ID: {ClientId}", license.Client_id ?? "N/A");

        // Token Information
        logger.LogDebug("Token Information:");
        logger.LogDebug("------------------");
        logger.LogDebug("Token Limit: {TokenLimit}", license.Token_limit);
        if (license.Goodwill_token_limit.HasValue) {
            logger.LogDebug("Goodwill Token Limit: {GoodwillTokenLimit}", license.Goodwill_token_limit.Value);
        }
        if (license.Floating_token_limit.HasValue) {
            logger.LogDebug("Floating Token Limit: {FloatingTokenLimit}", license.Floating_token_limit.Value);
        }
        if (license.User_limit.HasValue) {
            logger.LogDebug("User Limit: {UserLimit}", license.User_limit.Value);
        }

        // Customer Information
        var customer = license.Customer;
        if (customer != null) {
            logger.LogDebug("Customer Information:");
            logger.LogDebug("---------------------");
            logger.LogDebug("Customer ID: {CustomerId}", license.Customer_id);
            if (!string.IsNullOrEmpty(customer.Company_name)) {
                logger.LogDebug("Company Name: {CompanyName}", customer.Company_name);
            }
            if (!string.IsNullOrEmpty(customer.Customer_number)) {
                logger.LogDebug("Customer Number: {CustomerNumber}", customer.Customer_number);
            }
        }

        // License Validity Status
        logger.LogDebug("License Validity Status:");
        logger.LogDebug("-----------------------");

        bool isExpired = false;
        if (license.Expiration_date_utc.HasValue) {
            logger.LogDebug("Expiration Date: {ExpirationDate}", license.Expiration_date_utc.Value.ToString("yyyy-MM-dd HH:mm"));

            if (license.Expiration_date_utc.Value.Year >= 9999) {
                logger.LogDebug("This is a perpetual license.");
            } else {
                long daysRemaining = (license.Expiration_date_utc.Value - DateTime.UtcNow).Days;
                isExpired = daysRemaining < 0;

                if (isExpired) {
                    long expiredDays = Math.Abs(daysRemaining);
                    logger.LogDebug("License is expired since {ExpiredDays} day(s).", expiredDays);
                } else {
                    logger.LogDebug("License is valid for another {DaysRemaining} day(s) until {ExpirationDate}", daysRemaining, license.Expiration_date_utc.Value.ToString("yyyy-MM-dd HH:mm"));
                }
            }
        } else {
            logger.LogDebug("License has no expiration date.");
        }

        // License Status
        logger.LogDebug("License Status: {LicenseStatus}", isExpired ? "Expired" : "Active");

        // Features
        if (license.License_features != null && license.License_features.Count > 0) {
            logger.LogDebug("Features:");
            foreach (var feature in license.License_features) {
                logger.LogDebug("- {FeatureName} (Active: {IsActive})", feature.Feature_name ?? "", feature.Is_active);
            }
        } else {
            logger.LogDebug("No features available in this license.");
        }

        // Limitations
        var limitationMap = license.License_limitations?.ToDictionary(
            l => l.Limitation_id,
            l => $"{l.Limitation_name ?? ""} (max: {l.Limit})"
        ) ?? new Dictionary<Guid, string>();

        if (license.License_limitations != null && license.License_limitations.Count > 0) {
            logger.LogDebug("Limitations:");
            foreach (var limitation in license.License_limitations) {
                logger.LogDebug("- {LimitationName}: {Limit}", limitation.Limitation_name ?? "", limitation.Limit);
            }
        } else {
            logger.LogDebug("No limitations available in this license.");
        }

        return limitationMap;
    }

    /// <summary>
    /// Prints the details of a LicenseXmlDto object to the log.
    /// Displays license information, customer details, features, limitations, and variables.
    /// </summary>
    /// <param name="licenseXml">The license XML object to print</param>
    public void PrintLicenseDetails(LicenseXmlDto licenseXml) {
        if (licenseXml == null) {
            logger.LogDebug("No license information available.");
            return;
        }

        logger.LogDebug("License Information:");
        logger.LogDebug("-------------------");
        logger.LogDebug("License Name: {LicenseName}", licenseXml.LicenseName);
        logger.LogDebug("License Key: <secret>");
        logger.LogDebug("Legacy License Key: <secret>");

        // Customer Information
        var customer = licenseXml.Customer;
        if (customer != null) {
            logger.LogDebug("Customer Information:");
            logger.LogDebug("---------------------");
            logger.LogDebug("Customer ID: {CustomerId}", customer.CustomerId);
            logger.LogDebug("Company Name: {CompanyName}", customer.CompanyName);
            logger.LogDebug("Customer Number: {CustomerNumber}", customer.CustomerNumber);
        }

        // Product Information
        logger.LogDebug("Product Information:");
        logger.LogDebug("--------------------");
        logger.LogDebug("Product Name: {ProductName}", licenseXml.ProductName);
        logger.LogDebug("Product ID: {ProductId}", licenseXml.ProductId);
        logger.LogDebug("Template Name: {TemplateName}", licenseXml.TemplateName);

        // License Expiration Information
        logger.LogDebug("License Validity:");
        logger.LogDebug("-------------------");

        long daysRemaining = 0;
        if (licenseXml.ExpirationDateUtc.HasValue) {
            logger.LogDebug("Expiration Date: {ExpirationDate}", licenseXml.ExpirationDateUtc.Value.ToString("yyyy-MM-dd"));

            daysRemaining = (licenseXml.ExpirationDateUtc.Value - DateTime.UtcNow).Days;
            bool isExpired = daysRemaining < 0;

            if (isExpired) {
                long expiredDays = Math.Abs(daysRemaining);
                logger.LogDebug("License is expired since {ExpiredDays} day(s).", expiredDays);
            } else {
                logger.LogDebug("License is valid for another {DaysRemaining} day(s) until {ExpirationDate}", daysRemaining, licenseXml.ExpirationDateUtc.Value.ToString("yyyy-MM-dd"));
            }
        } else {
            logger.LogDebug("License has no expiration date (perpetual license).");
        }

        // License Status
        logger.LogDebug("License Status: {LicenseStatus}", (daysRemaining < 0 ? "Expired" : "Active"));

        // Features
        if (licenseXml.Features != null && licenseXml.Features.Count > 0) {
            logger.LogDebug("Features:");
            foreach (var feature in licenseXml.Features) {
                logger.LogDebug("- {FeatureName} (Active: {IsActive})", feature.Name, feature.IsActive);
            }
        } else {
            logger.LogDebug("No features available in this license.");
        }

        // Limitations
        if (licenseXml.Limitations != null && licenseXml.Limitations.Count > 0) {
            logger.LogDebug("Limitations:");
            foreach (var limitation in licenseXml.Limitations) {
                logger.LogDebug("- {LimitationName}: {LimitationValue}", limitation.Name, limitation.Value);
            }
        } else {
            logger.LogDebug("No limitations available in this license.");
        }
    }

    /// <summary>
    /// Prints the details of a LicenseInfoDto object to the log.
    /// Displays license status, features, limitations, and expiration information.
    /// </summary>
    /// <param name="licenseInfo">The LicenseInfoDto object to print</param>
    /// <returns>A dictionary of limitations for further use in the application</returns>
    public IDictionary<Guid, string> PrintLicenseDetails(LicenseInfoDto licenseInfo) {
        if (licenseInfo == null) {
            logger.LogDebug("No license information available.");
            return new Dictionary<Guid, string>();
        }

        logger.LogDebug("License infos (Retrieved {CreatedDate}):", licenseInfo.Created_date_utc);

        // License Information
        logger.LogDebug("License Information:");
        logger.LogDebug("-------------------");
        logger.LogDebug("License Name: {LicenseName}", licenseInfo.License_name ?? "");
        logger.LogDebug("License Key: <secret>");

        if (!string.IsNullOrEmpty(licenseInfo.Legacy_license_key)) {
            logger.LogDebug("Legacy License Key: <secret>");
        }

        if (licenseInfo.Token_key.HasValue) {
            logger.LogDebug("Token Key: <secret>");
        }

        // Customer Information
        var customer = licenseInfo.Customer;
        if (customer != null) {
            logger.LogDebug("Customer Information:");
            logger.LogDebug("---------------------");
            logger.LogDebug("Customer ID: {CustomerId}", customer.Customer_id.ToString() ?? "");
            logger.LogDebug("Company Name: {CompanyName}", customer.Company_name ?? "");
            logger.LogDebug("Customer Number: {CustomerNumber}", customer.Customer_number);
        }

        // Product Information
        logger.LogDebug("Product Information:");
        logger.LogDebug("--------------------");
        logger.LogDebug("Product Name: {ProductName}", licenseInfo.Product_name ?? "");
        logger.LogDebug("Template Name: {TemplateName}", licenseInfo.Template_name ?? "");
        logger.LogDebug("Provisioning mode / client type: {ProvisioningMode} / {ClientType}", licenseInfo.Provisioning_mode.ToString(), licenseInfo.Client_type.ToString());

        // License Details
        logger.LogDebug("License Details:");
        logger.LogDebug("----------------");
        logger.LogDebug("Provisioning Mode: {ProvisioningMode}", licenseInfo.Provisioning_mode);
        logger.LogDebug("Is Temporary: {IsTemporary}", licenseInfo.Is_temporary);
        logger.LogDebug("Heartbeat Period: {HeartbeatPeriod} days", licenseInfo.Heartbeat_period ?? 0);

        if (licenseInfo.Session_period.HasValue && licenseInfo.Session_period.Value > 0) {
            logger.LogDebug("Session Period: {SessionPeriod} days", licenseInfo.Session_period.Value);
        }

        // License Validity Status
        logger.LogDebug("License Validity Status:");
        logger.LogDebug("-----------------------");
        logger.LogDebug("License is {LicenseStatus} (IsActive: {IsActive}; IsExpired: {IsExpired})",
            (licenseInfo.Is_license_valid ? "valid" : "not valid"),
            licenseInfo.Is_license_active,
            licenseInfo.Is_license_expired);

        // Expiration Information
        logger.LogDebug("License Expiration Info:");
        if (licenseInfo.Expiration_date_utc.HasValue) {
            logger.LogDebug("Expiration Date: {ExpirationDate}", licenseInfo.Expiration_date_utc.Value.ToString("yyyy-MM-dd HH:mm"));
        } else {
            logger.LogDebug("License has no expiration date.");
        }

        // Software Version Information
        var swLimitation = licenseInfo.Software_release_limitation;
        if (swLimitation != null) {
            logger.LogDebug("Software Version Information:");
            logger.LogDebug("Is Software Version Valid: {IsSoftwareVersionValid}", licenseInfo.Is_software_version_valid);
            logger.LogDebug("Enforce Software Upgrade: {EnforceSoftwareUpgrade}", licenseInfo.Enforce_software_version_upgrade);

            if (!string.IsNullOrEmpty(swLimitation.Software_release)) {
                logger.LogDebug("Software Release: {SoftwareRelease}", swLimitation.Software_release);
            }

            if (!string.IsNullOrEmpty(swLimitation.Description)) {
                logger.LogDebug("Description: {Description}", swLimitation.Description);
            }
        }

        // Features
        if (licenseInfo.Features != null && licenseInfo.Features.Count > 0) {
            logger.LogDebug("Features:");
            foreach (var feature in licenseInfo.Features) {
                logger.LogDebug("- {FeatureName} (Active: {IsActive})", feature.Name ?? "", feature.Is_active);
                if (feature.Expiration_date_utc.HasValue) {
                    logger.LogDebug("  Expires: {ExpirationDate}", feature.Expiration_date_utc.Value.ToString("yyyy-MM-dd HH:mm"));
                }
            }
        } else {
            logger.LogDebug("No features available in this license.");
        }

        // Limitations
        var limitationMap = licenseInfo.Limitations?.ToDictionary(
            l => l.Id,
            l => $"{l.Name} (max: {l.Value})"
        ) ?? [];

        if (licenseInfo.Limitations != null && licenseInfo.Limitations.Count > 0) {
            logger.LogDebug("Limitations:");
            foreach (var limitation in licenseInfo.Limitations) {
                logger.LogDebug("- {LimitationName}: {LimitationValue}", limitation.Name ?? "", limitation.Value);
            }
        } else {
            logger.LogDebug("No limitations available in this license.");
        }

        return limitationMap;
    }
}

