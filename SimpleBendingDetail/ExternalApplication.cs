using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.IO;

namespace SimpleBendingDetail
{
    internal class ExternalApplication : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            //throw new NotImplementedException();
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            //throw new NotImplementedException();
            application.CreateRibbonTab("bimUtils");

            string path = Assembly.GetExecutingAssembly().Location;
            PushButtonData button = new PushButtonData("Button1", "Simple" + System.Environment.NewLine + "Bending Detail", path, "SimpleBendingDetail.SimpleBendingDetail");

            RibbonPanel panel = application.CreateRibbonPanel("bimUtils", "Reinforcement");


            //Add image
            //Uri imagePath = new Uri(@"D:\\bimUtils\\SimpleBendingDetail\icon.png");
            string iconPath = Path.GetDirectoryName(path) + "\\Resources\\SimpleBendingDetail.png";

            Uri imagePath = new Uri(@iconPath);
            BitmapImage image = new BitmapImage(imagePath);

            PushButton pushButton = panel.AddItem(button) as PushButton;
            pushButton.LargeImage = image;

            return Result.Succeeded;

        }
    }
}
