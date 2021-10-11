using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Websocket.Client;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace Mirror3
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        public byte[] imageBytes;
        public WebsocketClient client;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            this.Window.AddFlags(WindowManagerFlags.Fullscreen);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            Xamarin.Forms.Forms.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            SupportActionBar.Hide();

            int uiOptions = (int)Window.DecorView.SystemUiVisibility;

            uiOptions |= (int)SystemUiFlags.LowProfile;
            uiOptions |= (int)SystemUiFlags.Fullscreen;
            uiOptions |= (int)SystemUiFlags.HideNavigation;
            uiOptions |= (int)SystemUiFlags.ImmersiveSticky;

            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;

            ImageView image = FindViewById<ImageView>(Resource.Id.imageView1);

            image.Touch += Image_Touch;
            GestureDetector _gestureDetector = new GestureDetector(this, new GestureListener());

            _gestureDetector.DoubleTap += (object sender, GestureDetector.DoubleTapEventArgs e) => {
                if (client != null)
                {
                    JObject res = new JObject();
                    res["type"] = "touch";
                    client.Send(res.ToString());
                }
            };

            //apply touch to your view
            image.Touch += (object sender, Android.Views.View.TouchEventArgs e) => {
                _gestureDetector.OnTouchEvent(e.Event);
            };

            client = new WebsocketClient(new Uri("ws://192.168.1.109:8080"));
            client.ReconnectTimeout = null;
            client.MessageReceived.Subscribe(message =>
            {
                string text = message.Text.Trim();
                JObject msg = JObject.Parse(text);
                Console.WriteLine(text);
                if (msg["type"].ToString() == "image")
                {
                    imageBytes = Convert.FromBase64String(msg["data"].ToString());
                }
            });
            client.Start();


            Device.StartTimer(TimeSpan.FromMilliseconds(1), () => {

                if (imageBytes != null)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        image.SetImageBitmap(BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length));
                    });
                }
                
                return true;
            });
        }

        private void Image_Click(object sender, EventArgs e)
        {
            Console.WriteLine("clicked");
        }

        private void Image_Touch(object sender, Android.Views.View.TouchEventArgs e)
        {
            float x = e.Event.RawX;
            float y = e.Event.RawY;
            double ratioX = DeviceDisplay.MainDisplayInfo.Width / 422;
            double ratioY = DeviceDisplay.MainDisplayInfo.Height / 912;
            if (client != null)
            {
                JObject res = new JObject();
                res["type"] = "position";
                res["data"] = (x / ratioX) + "," + (y / ratioY);
                client.Send(res.ToString());
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}