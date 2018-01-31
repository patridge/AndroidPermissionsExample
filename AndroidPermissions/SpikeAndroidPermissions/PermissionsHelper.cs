using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SpikeAndroidPermissions
{
    public static class PermissionExtensions
    {
        public static Dictionary<string, Permission> AsDictionary(this string[] permissions, Permission[] results)
        {
            return permissions.Zip(results, (permission, result) => new { Key = permission, Value = result }).ToDictionary(x => x.Key, x => x.Value);
        }
    }
    public class PermissionsRequestConfiguration
    {
        public static PermissionsRequestConfiguration Default = new PermissionsRequestConfiguration()
        {
            RequestAlertMessage = "This app requires a few permissions.",
            RequestAlertPositiveButton = "Allow",
            RequestAlertNegativeButton = "No thanks",
            RequestAlertNegativeResponseAlertMessage = "Without these permissions, parts of this app may not function as expected.",
            RequestAlertNegativeResponseAlertAcknowledgementButton = "Okay",
        };

        public string RequestAlertMessage { get; set; }
        public string RequestAlertPositiveButton { get; set; }
        public string RequestAlertNegativeButton { get; set; }
        public string RequestAlertNegativeResponseAlertMessage { get; set; }
        public string RequestAlertNegativeResponseAlertAcknowledgementButton { get; set; }
    }
    public class PermissionsHelpers : IDisposable
    {
        readonly Activity currentActivity;
        TaskCompletionSource<Dictionary<string, Permission>> PermissionResultTaskCompletionSource { get; set; }
        const int PermissionRequestCode = 1;
        readonly string[] requiredPermissions;
        readonly PermissionsRequestConfiguration configuration;
        //public Task PermissionGrantedTask { get; set; }

        public PermissionsHelpers(Activity currentActivity, string[] requiredPermissions, PermissionsRequestConfiguration configuration)
        {
            this.currentActivity = currentActivity;
            this.requiredPermissions = requiredPermissions;
            this.configuration = configuration;
            //PermissionGrantedTask = PermissionResultTaskCompletionSource.Task;
        }

        bool hasUserBlockedUsFromRequestingCameraPermission = false;

        public bool AreAllRequiredPermissionsGranted()
        {
            return requiredPermissions.All(permission => currentActivity.CheckSelfPermission(permission) == Permission.Granted);
        }
        public bool ShouldShowPermissionRationale()
        {
            return requiredPermissions.Any(permission => currentActivity.ShouldShowRequestPermissionRationale(permission));
        }

        public void RequestPermissions()
        {
            currentActivity.RequestPermissions(requiredPermissions, PermissionRequestCode);
        }

        public void CheckAndRequestPermissions()
        {
            if ((int)Build.VERSION.SdkInt < 23)
            {
                // Permission in pre-23 Android is given at install-time.
                return;
            }

            if (AreAllRequiredPermissionsGranted())
            {
                // Runtime permissions have already been granted.
                return;
            }

            if (ShouldShowPermissionRationale())
            {
                // User previously rejected a permission we need. Show something to explain, with an option that will ask for permission again.
                ShowPermissionRationale();
            }
            else
            {
                //if (hasUserBlockedUsFromRequestingCameraPermission)
                //{
                //    // Requesting under "Don't ask again" mode will immediate return Permission.Denied in result handler.
                //    // User must explicitly give us permission in the app's Settings now.

                //    ShowCameraPermissionRationale(
                //        () =>
                //        {
                //            Log.Info(TAG, "Despite telling us to never ask again, we convinced them to allow camera use. Send them to Settings to allow it.");

                //            // TODO: Would be worth showing the user what they need to do in Settings to remedy the permission issue.
                //            //       "Under Permissions in the Android Settings for Awesome Camera, you'll have to enable the Camera permission."

                //            var intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings, Android.Net.Uri.FromParts("package", this.PackageName, null));
                //            intent.AddFlags(ActivityFlags.NewTask);
                //            StartActivity(intent);

                //            // We could set up an async delayed check for the permission here and try to re-launch our app, if allowed.
                //        },
                //        () =>
                //        {
                //            Log.Info(TAG, "They still _really_ don't want us to ask for camera permission.");
                //        }
                //    );
                //}
                //else
                //{
                    // Have Android ask for permissions we need.
                    // NOTE: Requesting under "Don't ask again" mode would immediate return Permission.Denied in result handler.
                    //       Same if the device has a policy against allowing a given permission.
                    RequestPermissions();
                //}
            }
        }

        void ShowPermissionRationale()
        {
            // Show something to explain, with an option that will ask for permission again.
            var permissionExplanationAlert = new AlertDialog.Builder(currentActivity)
                .SetMessage(configuration.RequestAlertMessage)
                .SetPositiveButton(configuration.RequestAlertPositiveButton, (sender, args) =>
                {
                    // User convinced to let us ask again. Have Android ask for permissions we need.
                    RequestPermissions();
                })
                .SetNegativeButton(configuration.RequestAlertNegativeButton, (sender, args) =>
                {
                    var cannotProceedWithoutPermissionAlert = new AlertDialog.Builder(currentActivity)
                            .SetMessage(configuration.RequestAlertNegativeResponseAlertMessage)
                            .SetPositiveButton(configuration.RequestAlertNegativeResponseAlertAcknowledgementButton, (s, a) => { })
                            .Create();
                    cannotProceedWithoutPermissionAlert.Show();
                })
                .Create();
            permissionExplanationAlert.Show();
        }

        public void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            switch (requestCode)
            {
                // Use a request code to know what to look for in the permissions/grantResult arrays.
                case PermissionRequestCode:
                    var results = permissions.AsDictionary(grantResults);
                    PermissionResultTaskCompletionSource.TrySetResult(results);

                    //if (permissions.Length > 0
                    //    && grantResults[0] == Permission.Denied)
                    //{
                    //    if (!ShouldShowRequestPermissionRationale(Manifest.Permission.Camera))
                    //    {
                    //        Log.Info(TAG, "(And we were blocked from asking for permission through Android again without direct Settings intervention.)");

                    //        hasUserBlockedUsFromRequestingCameraPermission = true;
                    //    }
                    //}

                    // TODO: Potentially disable any functionality that relies on that denied permission.

                    break;
                default:
                    break;
            }
        }

        public void Dispose()
        {
            PermissionResultTaskCompletionSource.TrySetCanceled();
        }
    }
}