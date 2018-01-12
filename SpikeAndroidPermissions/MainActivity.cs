using Android.App;
using Android.Widget;
using Android.OS;
using Android;
using Android.Content.PM;
using System.Threading.Tasks;
using Android.Util;
using Android.Content;
using System;

namespace SpikeAndroidPermissions
{
    [Activity(Label = "Awesome Camera", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity
    {
        public const string TAG = nameof(MainActivity);

        // Dangerous permissions docs: https://developer.android.com/guide/topics/permissions/requesting.html#normal-dangerous
        //   * > All dangerous Android system permissions belong to permission groups. If the device is running Android 6.0 (API level 23) and the app's targetSdkVersion is 23 or higher, the following system behavior applies when your app requests a dangerous permission:
        //   * > Note: If the user turned down the permission request in the past and chose the Don't ask again option in the permission request system dialog, this method returns false. The method also returns false if a device policy prohibits the app from having that permission
        //   * > Note: Your app still needs to explicitly request every permission it needs, even if the user has already granted another permission in the same group. In addition, the grouping of permissions into groups may change in future Android releases. Your code should not rely on the assumption that particular permissions are or are not in the same group.

        // ?Toggling permission off in Settings seems to kill app debug session
        // Toggling permission on in Settings doesn't kill Debug session

        // ID passed in to result handler to know what request we are handling. (Google docs just say >= 0)
        // Docs: https://developer.android.com/reference/android/support/v4/app/ActivityCompat.html#requestPermissions(android.app.Activity, java.lang.String[], int)
        const int ButtonClickCameraPermissionRequestCode = 1;
        Button takePictureButton;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Main);

            takePictureButton = FindViewById<Button>(Resource.Id.takePictureButton);
            takePictureButton.Click += delegate {
                AskForCameraPermission();
            };
        }

        public void ShowCameraPermissionRationale(Action allowAction, Action denyAction)
        {
            // Show something to explain, with an option that will ask for permission again.
            var permissionExplanationAlert = new AlertDialog.Builder(this)
                .SetMessage("AwesomeCameraApp can't take pictures without permission to use the camera. Can we have permission to use your camera?")
                .SetPositiveButton("Allow camera use", (sender, args) =>
                {
                    allowAction();
                })
                .SetNegativeButton("No thanks", (sender, args) =>
                {
                    denyAction();
                })
                .Create();
            permissionExplanationAlert.Show();
        }
        public void AskForCameraPermission()
        {
            if (CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted)
            {
                // If we need multiple permissions, we would check against all of them.
                if (ShouldShowRequestPermissionRationale(Manifest.Permission.Camera))
                {
                    // If user has previously denied and we ask again, we should justify why we need the permission before the next request.
                    // Also, if user has explicitly denied our app the given permission in the Settings app (even if you haven't asked yet).
                    // (e.g., "You keep clicking "DENY" on camera permission, but you also keep clicking the camera button!")
                    // > (Android docs) Note: If the user turned down the permission request in the past and chose the **Don't ask again** option in the permission request system dialog, this method returns false. The method also returns false if a device policy prohibits the app from having that permission.
                    Log.Info(TAG, "Need to show permission explanation.");

                    ShowCameraPermissionRationale(
                        () => {
                            Log.Info(TAG, "Successfully convinced user to reconsider. Ask again via Android.");

                            RequestPermissions(new[] { Manifest.Permission.Camera }, ButtonClickCameraPermissionRequestCode);
                        },
                        () => {
                            Log.Info(TAG, "Couldn't convince them to allow camera use. Might be time to disable this feature.");

                            var cannotProceedWithoutPermissionAlert = new AlertDialog.Builder(this)
                                .SetMessage("Without permission to use the camera, you can't take pictures.")
                                .SetPositiveButton("Okay", (s, a) => { })
                                .Create();
                            cannotProceedWithoutPermissionAlert.Show();
                        }
                    );
                }
                else
                {
                    if (hasUserBlockedUsFromRequestingCameraPermission)
                    {
                        // Requesting under "Don't ask again" mode will immediate return Permission.Denied in result handler.
                        // User must explicitly give us permission in the app's Settings now.

                        ShowCameraPermissionRationale(
                            () =>
                            {
                                Log.Info(TAG, "Despite telling us to never ask again, we convinced them to allow camera use. Send them to Settings to allow it.");

                                // TODO: Would be worth showing the user what they need to do in Settings to remedy the permission issue.
                                //       "Under Permissions in the Android Settings for Awesome Camera, you'll have to enable the Camera permission."

                                var intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings, Android.Net.Uri.FromParts("package", this.PackageName, null));
                                intent.AddFlags(ActivityFlags.NewTask);
                                StartActivity(intent);

                                // We could set up an async delayed check for the permission here and try to re-launch our app, if allowed.
                            },
                            () =>
                            {
                                Log.Info(TAG, "They still _really_ don't want us to ask for camera permission.");
                            }
                        );
                    }
                    else
                    {
                        Log.Info(TAG, "Can just ask for permission.");

                        // NOTE: Requesting under "Don't ask again" mode would immediate return Permission.Denied in result handler.
                        //       Same if the device has a policy against allowing a given permission.
                        RequestPermissions(new[] { Manifest.Permission.Camera }, ButtonClickCameraPermissionRequestCode);
                    }
                }
            }
            else
            {
                Log.Info(TAG, "Already have permission.");

                TakePicture();
            }
        }

        public void TakePicture()
        {
            // Do camera stuff!

            takePictureButton.Text = "*shutter noise*";
            Task.Run(async () =>
            {
                await Task.Delay(1500);
                RunOnUiThread(() => takePictureButton.Text = Resources.GetString(Resource.String.takePictureButtonDefaultText));
            });
        }

        bool hasUserBlockedUsFromRequestingCameraPermission = false;
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            switch (requestCode)
            {
                // Use a request code to know what to look for in the permissions/grantResult arrays.
                case ButtonClickCameraPermissionRequestCode:
                    if (permissions.Length == 0)
                    {
                        // TODO: How does one cancel? (Doesn't fire for home button and back button doesn't do anything.)
                        Log.Info(TAG, "Request dialog was cancelled.");
                        // TODO: Once we figure out how to trigger it, figure out what an app might do knowing it was cancelled.
                    }
                    if (permissions.Length > 0
                        && grantResults[0] == Permission.Granted)
                    {
                        Log.Info(TAG, "Permission was granted on Android dialog.");

                        TakePicture();
                    }
                    else
                    {
                        Log.Info(TAG, "Permission was denied on Android dialog or we were blocked from asking (or device policy forbids it).");

                        if (!ShouldShowRequestPermissionRationale(Manifest.Permission.Camera))
                        {
                            Log.Info(TAG, "(And we were blocked from asking for permission through Android again without direct Settings intervention.)");

                            hasUserBlockedUsFromRequestingCameraPermission = true;
                        }

                        // TODO: Potentially disable any functionality that relies on that denied permission.
                    }
                    break;
                default:
                    Log.Info(TAG, "Unknown permission request code.");
                    break;
            }
        }
    }
}

