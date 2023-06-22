using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using WakeVUpCompanion.Droid;

[assembly: Xamarin.Forms.Dependency(typeof(AndroidToast))]
namespace WakeVUpCompanion.Droid
{
    public class AndroidToast : IToast
    {
        public void ShowToast(string message)
        {
            var toast = Toast.MakeText(Application.Context, message, ToastLength.Short);
            toast.Show();
        }
    }
}