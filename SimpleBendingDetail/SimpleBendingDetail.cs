using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;

namespace SimpleBendingDetail
{
    [TransactionAttribute(TransactionMode.ReadOnly)]
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

            //Create Plane of View
            //to do Remove after functions are ready
            //Plane plane = Plane.CreateByOriginAndBasis(view.Origin, view.RightDirection, view.UpDirection);

            //Get Shared parameters ID for rebars in document
            //IList<ElementId> shapeParametersIds = RebarShapeParameters.GetAllRebarShapeParameters(doc);


            //Select rebars
            //todo: filter only rebars; filter only shape driven rebars

            Selection selection = uidoc.Selection;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            //IList<Element> parameterElements = new List<Element>();

            //foreach (var item in shapeParametersIds)
            //{
            //    Element el = doc.GetElement(item);
            //    parameterElements.Add(el);
            //}

            IList<Rebar> rebars = new List<Rebar>();

            foreach (ElementId id in selectedIds)
            {
                Rebar singleRebar = doc.GetElement(id) as Rebar;
                rebars.Add(singleRebar);
            }


            try
            {


                using (Transaction trans = new Transaction(doc, "Curves Placed"))
                {
                    trans.Start();


                    //get family parameters for each selected rebar    
                    foreach (var rebar in rebars)
                    {


                         BendindDetail bendingDetail = new BendindDetail(doc, view, rebar);


                        //Create Filtered Element Collector
                        FilteredElementCollector collector = new FilteredElementCollector(doc);
                        collector.OfCategory(BuiltInCategory.OST_DetailComponents);
                        collector.OfClass(typeof(FamilySymbol));

                        FamilySymbol familySymbol = collector.WhereElementIsElementType()
                            .Cast<FamilySymbol>()
                            .First(x => x.Name == "Simple_Bending_Detail"); // Simple_Bending_Detail  Floating_Column_Detail

                        bendingDetail.PlaceOnView(doc, view, familySymbol);



                    }//for each rebar in rebars


                    



                    trans.Commit();


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
