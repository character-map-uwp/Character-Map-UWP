// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// src: Windows Community Toolkit, v6.1.0.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.System;
using Windows.System.Profile;
using Windows.System.UserProfile;

namespace CharacterMap.Helpers
{
    /// <summary>
    /// Defines Operating System version
    /// </summary>
    public struct OSVersion
    {
        /// <summary>
        /// Value describing major version
        /// </summary>
        public ushort Major;

        /// <summary>
        /// Value describing minor version
        /// </summary>
        public ushort Minor;

        /// <summary>
        /// Value describing build
        /// </summary>
        public ushort Build;

        /// <summary>
        /// Value describing revision
        /// </summary>
        public ushort Revision;

        /// <summary>
        /// Converts OSVersion to string
        /// </summary>
        /// <returns>Major.Minor.Build.Revision as a string</returns>
        public override string ToString()
        {
            return $"{Major}.{Minor}.{Build}.{Revision}";
        }
    }

    /// <summary>
    /// This class provides static helper methods for <see cref="PackageVersion"/>.
    /// </summary>
    public static class PackageVersionHelper
    {
        /// <summary>
        /// Returns a string representation of a version with the format 'Major.Minor.Build.Revision'.
        /// </summary>
        /// <param name="packageVersion">The <see cref="PackageVersion"/> to convert to a string</param>
        /// <returns>Version string of the format 'Major.Minor.Build.Revision'</returns>
        public static string ToFormattedString(this PackageVersion packageVersion)
        {
            return $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
        }

        /// <summary>
        /// Converts a string representation of a version number to an equivalent <see cref="PackageVersion"/>.
        /// </summary>
        /// <param name="formattedVersionNumber">Version string of the format 'Major.Minor.Build.Revision'</param>
        /// <returns>The parsed <see cref="PackageVersion"/></returns>
        public static PackageVersion ToPackageVersion(this string formattedVersionNumber)
        {
            var parts = formattedVersionNumber.Split('.');

            return new PackageVersion
            {
                Major = ushort.Parse(parts[0]),
                Minor = ushort.Parse(parts[1]),
                Build = ushort.Parse(parts[2]),
                Revision = ushort.Parse(parts[3])
            };
        }
    }

    /// <summary>
    /// This class provides info about the app and the system.
    /// </summary>
    public sealed class SystemInformation
    {
        /// <summary>
        /// Launches the store app so the user can leave a review.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>This method needs to be called from your UI thread.</remarks>
        public static async Task LaunchStoreForReviewAsync()
        {
            await Launcher.LaunchUriAsync(new Uri(string.Format("ms-windows-store://review/?PFN={0}", Package.Current.Id.FamilyName)));
        }

        /// <summary>
        /// Gets the unique instance of <see cref="SystemInformation"/>.
        /// </summary>
        public static SystemInformation Instance { get; } = new SystemInformation();

        /// <summary>
        /// Gets the application's name.
        /// </summary>
        public string ApplicationName { get; }

        /// <summary>
        /// Gets the application's version.
        /// </summary>
        public PackageVersion ApplicationVersion { get; }

        /// <summary>
        /// Gets the user's most preferred culture.
        /// </summary>
        public CultureInfo Culture { get; }

        /// <summary>
        /// Gets the device's family.
        /// <para></para>
        /// Common values include:
        /// <list type="bullet">
        /// <item><term>"Windows.Desktop"</term></item>
        /// <item><term>"Windows.Mobile"</term></item>
        /// <item><term>"Windows.Xbox"</term></item>
        /// <item><term>"Windows.Holographic"</term></item>
        /// <item><term>"Windows.Team"</term></item>
        /// <item><term>"Windows.IoT"</term></item>
        /// </list>
        /// <para></para>
        /// Prepare your code for other values.
        /// </summary>
        public string DeviceFamily { get; }

        /// <summary>
        /// Gets the operating system's name.
        /// </summary>
        public string OperatingSystem { get; }

        /// <summary>
        /// Gets the operating system's version.
        /// </summary>
        public OSVersion OperatingSystemVersion { get; }

        /// <summary>
        /// Gets the processor architecture.
        /// </summary>
        public ProcessorArchitecture OperatingSystemArchitecture { get; }

        /// <summary>
        /// Gets the device's model.
        /// Will be empty if the model couldn't be determined (For example: when running in a virtual machine).
        /// </summary>
        public string DeviceModel { get; }

        /// <summary>
        /// Gets the device's manufacturer.
        /// Will be empty if the manufacturer couldn't be determined (For example: when running in a virtual machine).
        /// </summary>
        public string DeviceManufacturer { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemInformation"/> class.
        /// </summary>
        private SystemInformation()
        {
            ApplicationName = Package.Current.DisplayName;
            ApplicationVersion = Package.Current.Id.Version;
            try
            {
                Culture = GlobalizationPreferences.Languages.Count > 0 ? new CultureInfo(GlobalizationPreferences.Languages.First()) : null;
            }
            catch
            {
                Culture = null;
            }

            DeviceFamily = AnalyticsInfo.VersionInfo.DeviceFamily;
            ulong version = ulong.Parse(AnalyticsInfo.VersionInfo.DeviceFamilyVersion);
            OperatingSystemVersion = new OSVersion
            {
                Major = (ushort)((version & 0xFFFF000000000000L) >> 48),
                Minor = (ushort)((version & 0x0000FFFF00000000L) >> 32),
                Build = (ushort)((version & 0x00000000FFFF0000L) >> 16),
                Revision = (ushort)(version & 0x000000000000FFFFL)
            };
            OperatingSystemArchitecture = Package.Current.Id.Architecture;
        }
    }
}