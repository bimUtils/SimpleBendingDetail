using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;
using System.Reflection;
using System.IO;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;

namespace SimpleBendingDetail
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class SimpleBendingDetail : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Get UIDocument
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            //Get Document
            Document doc = uidoc.Document;

            //Get Current View
            View view = doc.ActiveView;


            Selection selection = uidoc.Selection;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();


            //filter selection to rebars only
            ICollection<ElementId> selectedRebarIds = new List<ElementId>();
            foreach (ElementId id in selectedIds)
            {
                Element ele = uidoc.Document.GetElement(id);
                if (ele is Rebar)
                {
                    selectedRebarIds.Add(id);
                }
            }

            // Set the created element set as current select element set.
            uidoc.Selection.SetElementIds(selectedRebarIds);




            //IList<Element> parameterElements = new List<Element>();

            //foreach (var item in shapeParametersIds)
            //{
            //    Element el = doc.GetElement(item);
            //    parameterElements.Add(el);
            //}

            IList<Rebar> rebars = new List<Rebar>();


            //****************
            //Get only planar rebars with shape
            //****************
            int unsupportedRebars = 0;
            foreach (ElementId id in selectedRebarIds)
            {
                Rebar singleRebar = doc.GetElement(id) as Rebar;


                //check for freeform rebar without shape

                if (singleRebar.IsRebarFreeForm())
                {

                    unsupportedRebars++;
                    continue;

                    //todo - pass freeform rebars with shape
                    //if (singleRebar.GetFreeFormAccessor().WorkshopInstructions == RebarWorkInstructions.Straight)
                    //{
                    //    unsupportedRebars++;
                    //    continue;
                    //}

                }

                //check for multiplanar rebars

                IList<Curve> curves = singleRebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, 0).ToList();
                CurveLoop curveLoop = new CurveLoop();
                foreach (Curve curve in curves)
                {
                    curveLoop.Append(curve);
                }
                if (!curveLoop.HasPlane() && curves.Count > 1)
                {
                    unsupportedRebars++;
                    continue;
                }



                rebars.Add(singleRebar);
            }

            if (unsupportedRebars > 0)
            {
                TaskDialog.Show("Information", $"{unsupportedRebars.ToString()} unsuppported 3D or free form rebar(s) found.");
            }



            try
            {


                using (Transaction trans = new Transaction(doc, "Curves Placed"))
                {
                    trans.Start();

                    ICollection<ElementId> placedDetIds = new List<ElementId>();

                    //Create Filtered Element Collector
                    FilteredElementCollector collector = new FilteredElementCollector(doc);
                    collector.OfCategory(BuiltInCategory.OST_DetailComponents);
                    collector.OfClass(typeof(FamilySymbol));

                    //check if family exist in the project, if not - loads family
                    var exists = from FamilySymbol fs in collector
                                 where fs.Name.Equals("Simple_Bending_Detail") && fs.FamilyName.Equals("Simple_Bending_Detail")
                                 select fs;

                    if (exists.Count() == 0)
                    {
                        //load family
                        string pathFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        //string famPath = "D:\\bimUtils" + "\\Simple_Bending_Detail.rfa";
                        string famPath = pathFolder + "\\Simple_Bending_Detail.rfa";
                        if (!doc.LoadFamilySymbol(@famPath, "Simple_Bending_Detail"))
                        {
                            TaskDialog.Show("bimUtils", "Family not found!");
                        }
                    }

                    FamilySymbol familySymbol = collector.WhereElementIsElementType()
                            .Cast<FamilySymbol>()
                            .First(x => x.Name == "Simple_Bending_Detail"); // Simple_Bending_Detail  Floating_Column_Detail


                    //get family parameters for each selected rebar    
                    foreach (var rebar in rebars)
                    {


                        BendindDetail bendingDetail = new BendindDetail(doc, view, rebar);

                        ElementId detId = bendingDetail.PlaceOnView(doc, view, familySymbol);
                        placedDetIds.Add(detId);

                    }//for each rebar in rebars




                    trans.Commit();

                    if (placedDetIds.Count > 0)
                    {
                        uidoc.Selection.SetElementIds(placedDetIds);

                    }



                }







                return Result.Succeeded;


            }
            catch (Exception e)
            {
                message = e.Message;
                return Result.Failed;
            }
        }

















    } //end class BendingDetail

}//end namespace
