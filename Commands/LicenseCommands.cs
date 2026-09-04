// Ported from arcgis-mcp v0.6.0 (MIT, Marco Gonzalez Valdiviezo), verbatim logic.
// License/extension probing mirrors arcpy.CheckExtension semantics.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Licensing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace LibreMcpAddin.Commands
{
    public static class LicenseCommands
    {
        private static readonly string[] ExtensionCodes =
        {
            "3D", "Spatial", "Network", "GeoStats", "ImageAnalyst",
            "DataReviewer", "DataInteroperability", "Airports", "Aeronautical",
            "Bathymetry", "BusinessPrem", "Defense", "Foundation", "Indoors",
            "LocationReferencing", "LocateXT", "Nautical", "Publisher",
            "SMPNorthAmerica", "SMPEurope", "SMPAsiaPacific", "SMPJapan",
            "SMPLatinAmerica", "SMPMiddleEastAfrica", "ArcScan", "Schematics",
            "Tracking", "JTX"
        };

        private static readonly Dictionary<string, string> CodeToLicenseCodes = new Dictionary<string, string>
        {
            { "3D", "Analyst3D" },
            { "Spatial", "SpatialAnalyst" },
            { "Network", "NetworkAnalyst" },
            { "GeoStats", "GeostatisticalAnalyst" },
            { "ImageAnalyst", "ImageAnalyst" },
            { "DataReviewer", "DataReviewer" },
            { "DataInteroperability", "DataInteroperability" },
            { "Airports", "AviationAirports" },
            { "Aeronautical", "AviationCharting" },
            { "Bathymetry", "Bathymetry" },
            { "BusinessPrem", "BusinessAnalyst" },
            { "Defense", "DefenseMapping" },
            { "Foundation", "Foundation" },
            { "Indoors", "Indoors" },
            { "LocationReferencing", "LocationReferencing" },
            { "LocateXT", "LocateXT" },
            { "Nautical", "MaritimeCharting" },
            { "Publisher", "Publisher" },
            { "SMPNorthAmerica", "StreetMapPremiumNorthAmerica" },
            { "SMPEurope", "StreetMapPremiumEurope" },
            { "SMPAsiaPacific", "StreetMapPremiumAsiaPacific" },
            { "SMPJapan", "StreetMapPremiumJapan" },
            { "SMPLatinAmerica", "StreetMapPremiumLatinAmerica" },
            { "SMPMiddleEastAfrica", "StreetMapPremiumMiddleEastAfrica" },
            { "ArcScan", "ArcScan" },
            { "Schematics", "Schematics" },
            { "Tracking", "TrackingAnalyst" },
            { "JTX", "WorkflowManager" }
        };

        public static Task<object> CheckLicenseAsync()
        {
            return QueuedTask.Run<object>(() =>
            {
                var level = LicenseInformation.Level.ToString();
                var active = new List<string>();

                foreach (var code in ExtensionCodes)
                {
                    if (IsExtensionCheckedOut(code))
                    {
                        active.Add(code);
                    }
                }

                return new
                {
                    level = level,
                    product = level,
                    extensions = active
                };
            });
        }

        private static bool IsExtensionCheckedOut(string arcpyCode)
        {
            if (!CodeToLicenseCodes.TryGetValue(arcpyCode, out var enumName))
            {
                return false;
            }

            try
            {
                if (Enum.TryParse(typeof(LicenseCodes), enumName, out var parsed) &&
                    parsed is LicenseCodes code)
                {
                    return LicenseInformation.IsCheckedOut(code);
                }
            }
            catch
            {
                // Some enum members only exist on newer Pro builds; treat as
                // unavailable rather than crashing the whole license probe.
            }

            return false;
        }
    }
}
