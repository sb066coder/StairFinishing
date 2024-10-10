using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Architecture;
using Nice3point.Revit.Toolkit.External;
using StairFinishing.ViewModels;
using StairFinishing.Views;
using StairFinishing.Models;
using System.Collections.Generic;

namespace StairFinishing.Commands
    #nullable enable
{
    /// <summary>
    ///     External command entry point invoked from the Revit interface
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class ReceiveFinishingArea : ExternalCommand
    {
        /**
        !- Проступи_площадь
        !- Подступенки_площадь
        !- Площадки_площадь
        !- Нижние_поверхности_маршей_площадь
        !- Нижние_поверхности_площадок_площадь
        !- Боковые_поверхности_площадь
        !- Плинтус_площадь
        !- Номер_Помещения, Имя_Помещения
        !- Дата, время, пользователь
         */
        public IEnumerable<Room> allRooms;
        public override void Execute()
        {
            allRooms = GetAllRooms();
            // Получает список лестниц
            IEnumerable<Element> stairsElements = UiDocument.Selection
                .GetElementIds()
                .Select(Document.GetElement)
                .Where(e => e is Stairs);
            IEnumerable<StairData> stairDataList = stairsElements.Select(e => new StairData(this, e.Cast<Stairs>()));

            DateTime moscowTime = DateTime.UtcNow.AddHours(3.0);
            string userName = Application.Username;

            using (Transaction t = new Transaction(Document, "Отделка лестниц"))
            {
                t.Start();
                foreach (StairData stairData in stairDataList)
                {
                    Parameter dogmaTreadsArea = stairData.stair.get_Parameter(new Guid("c480eab1-cff3-4ca2-bf42-724dcb7c405c"));
                    if (!dogmaTreadsArea.IsReadOnly)
                        dogmaTreadsArea.Set(stairData.treadsArea);

                    Parameter dogmaRisersArea = stairData.stair.get_Parameter(new Guid("88c98b48-be02-46e9-901a-79b4e367567d"));
                    if (!dogmaRisersArea.IsReadOnly)
                        dogmaRisersArea.Set(stairData.risersArea);

                    Parameter dogmaLandingsArea = stairData.stair.get_Parameter(new Guid("e248c91a-29fa-4ff5-93fc-f699d631cd23"));
                    if (!dogmaLandingsArea.IsReadOnly)
                        dogmaLandingsArea.Set(stairData.landingsArea);

                    Parameter dogmaRunLowerFacesArea = stairData.stair.get_Parameter(new Guid("e9816ced-5d3e-4f7f-922f-d3e311616b91"));
                    if (!dogmaRunLowerFacesArea.IsReadOnly)
                        dogmaRunLowerFacesArea.Set(stairData.runLowerFacesArea);

                    Parameter dogmaLandingLowerFacesArea = stairData.stair.get_Parameter(new Guid("a1fe07a1-b2d7-4cfc-b3e8-181b62f1ac8c"));
                    if (!dogmaLandingLowerFacesArea.IsReadOnly)
                        dogmaLandingLowerFacesArea.Set(stairData.landingLowerFacesArea);

                    Parameter dogmaRoomNumber = stairData.stair.get_Parameter(new Guid("7dd0843e-3041-46d3-9960-d3471139d6e5"));
                    if (!dogmaRoomNumber.IsReadOnly)
                        dogmaRoomNumber.Set(stairData.roomNumber != null ? stairData.roomNumber : "");

                    Parameter dogmaRoomName = stairData.stair.get_Parameter(new Guid("c498ebf6-f0ff-4453-84a0-b9741bd3af7e"));
                    if (!dogmaRoomName.IsReadOnly)
                        dogmaRoomName.Set(stairData.roomName != null ? stairData.roomName : "");
                    
                    Parameter dogmaSideFacesFinishingArea = stairData.stair.get_Parameter(new Guid("afbd7802-fd49-47e2-8033-35bbbe0ebb8e"));
                    if (!dogmaSideFacesFinishingArea.IsReadOnly)
                        dogmaSideFacesFinishingArea.Set(stairData.sideFacesFinishingArea);

                    Parameter dogmaSkirtingsArea = stairData.stair.get_Parameter(new Guid("faf05b87-3738-4eae-9d24-b69fdb3d1975"));
                    if (!dogmaSkirtingsArea.IsReadOnly)
                        dogmaSkirtingsArea.Set(stairData.skirtingsArea);

                    Parameter dogmaRefreshedPar = stairData.stair.get_Parameter(new Guid("a658a20e-8f11-48e3-b970-f0d6752f4f50"));
                    if (!dogmaRefreshedPar.IsReadOnly)
                        dogmaRefreshedPar.Set(moscowTime.ToString("yyyy-MM-dd HH:mm:ss") + " МСК - " + userName);
                    
                }
                t.Commit();
            }
            
            var viewModel = new StairFinishingViewModel();
            var view = new StairFinishingView(viewModel);
            view.ShowDialog();
        }

        private IEnumerable<Room> GetAllRooms()
        {
            return new FilteredElementCollector(Document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .ToList()
                .Cast<Room>();
        }
    }
}