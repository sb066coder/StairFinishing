using Autodesk.Revit.DB.Architecture;
using StairFinishing.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB.IFC;

namespace StairFinishing.Models
{
    #nullable enable
    internal class StairData
    {
        ReceiveFinishingArea receiveFinishingArea;
        internal Stairs stair { get; }
        Room? room { get; }
        internal double treadsArea;
        internal double risersArea;
        internal double landingsArea;
        internal double runLowerFacesArea;
        internal double landingLowerFacesArea;
        internal string? roomNumber;
        internal string? roomName;
        private double treadDepth;
        private double riserHeight;
        private double treadThickness;
        IEnumerable<IGrouping<bool, Face>> sideFacesWithAndWithoutFinishing;
        internal double sideFacesFinishingArea;
        internal double skirtingsArea;


        public StairData(ReceiveFinishingArea rfa, Stairs stair)
        {
            this.stair = stair;
            receiveFinishingArea = rfa;
            room = GetRoom();
            treadDepth = stair.FindParameter(BuiltInParameter.STAIRS_ACTUAL_TREAD_DEPTH).AsDouble();
            riserHeight = stair.FindParameter(BuiltInParameter.STAIRS_ACTUAL_RISER_HEIGHT).AsDouble();
            treadsArea = GetTreadsArea();
            risersArea = GetRisersArea();
            landingsArea = GetLandingsArea();
            runLowerFacesArea = GetRunLowerFacesArea();
            landingLowerFacesArea = GetLandingLowerFacesArea();
            roomNumber = GetRoomNumber();
            roomName = GetRoomName();
            sideFacesFinishingArea = GetSideFacesArea();
            skirtingsArea = GetSkirtingsArea();
        }

        private double GetTreadsArea()
        {
            double result = 0.0;
            IEnumerable<StairsRun> stairsRuns = GetRunsElements();
            foreach (StairsRun run in stairsRuns)
            {
                var runCL = run.GetFootprintBoundary();
                IList<CurveLoop> loopsList = [runCL];
                result += ExporterIFCUtils.ComputeAreaOfCurveLoops(loopsList);
                
            }
            return result;
        }

        private double GetRisersArea()
        {
            double result = 0.0;
            IEnumerable<StairsRun> stairsRuns = GetRunsElements();
            treadThickness = receiveFinishingArea.Document.GetElement(stairsRuns.First().GetTypeId())
                .FindParameter(BuiltInParameter.STAIRS_TRISERTYPE_TREAD_THICKNESS).AsDouble();
            foreach (StairsRun run in stairsRuns)
            {
                result += run.ActualRunWidth * run.Height;
            }
            // Возвращает сумму площадей
            return result;
        }

        private double GetLandingsArea()
        {
            double result = 0.0;
            var stairsLandings = GetLandingsElements();
            foreach (StairsLanding landing in stairsLandings)
            {
                foreach (Face face in GetStairElementFinishingFaces(landing))
                {
                    var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
                    if (faceNormal.Z.IsAlmostEqual(1.0))  // Если плоскость направлена вверх
                        result += face.Area;  // Берет площадь грани
                }
            }
            return result; // Возвращает сумму площадей
        }

        private double GetRunLowerFacesArea()
        {
            double result = 0.0;
            double treadDepth = stair.FindParameter(BuiltInParameter.STAIRS_ACTUAL_TREAD_DEPTH).AsDouble();
            double riserHeight = stair.FindParameter(BuiltInParameter.STAIRS_ACTUAL_RISER_HEIGHT).AsDouble();
            double slopeRatio = riserHeight / treadDepth; // Уклон марша
            double normalDirection = -Math.Cos(Math.Atan(slopeRatio)); // Значение Z нормали нижней поверхности марша
            IEnumerable<StairsRun> stairsRuns = GetRunsElements();
            foreach (StairsRun run in stairsRuns)
            {
                if (run.StairsRunStyle == StairsRunStyle.Spiral)
                {
                    foreach (Face face in GetStairElementFaces(run))
                    {
                        if (face is RuledFace)
                            result += face.Area;
                    }
                } 
                else
                {
                    foreach (Face face in GetStairElementFaces(run))
                    {
                        var faceNormal = face.ComputeNormal(UV.Zero);
                        if (faceNormal.Z.IsAlmostEqual(normalDirection)) // Если плоскость имеет такой же уклон, как марш
                            result += face.Area; // Берет площадь грани
                    }
                }
            }
            return result; // Возвращает сумму площадей
        }

        private double GetLandingLowerFacesArea()
        {
            double result = 0.0;
            IEnumerable<StairsLanding> stairsLandings = GetLandingsElements();
            foreach(StairsLanding landing in stairsLandings)
            {
                foreach (Face face in GetStairElementFaces(landing))
                {
                    if (face.ComputeNormal(new UV(0.5, 0.5)).Z.IsAlmostEqual(-1.0)) // Если грань обращена вниз
                        result += face.Area; // Берет площадь грани
                }
            }
            return result; // Возвращает сумму площадей
        }

        private double GetSideFacesArea()
        {
            /**
             * Получает геометрию лестницы - марши, площадки в виде солидов
             * Получает все грани
             * Получает пути лестницы из маршей и площадок
             * Находит пересечения путей и проекции граней
             * Берет все грани по ребрам с предыдущего шага
             * Отделяет от всех граней грани с предыдущего шага
             * Берет толко вертикальные грани
             * Строит точку на расстоянии 2 дюйма (запас на погрешности в построении модели) от центра грани и проверяет, находится ли эта точка в помещении
             * Группирует грани на пристеночные и внурти помещения и кладет в список для дальнейшего расчета плинтуса
             * Возвращает сумму площадей внутренних граней
             */

            List<Element> stairElements = [];
            IEnumerable<StairsRun> runs = GetRunsElements();
            IEnumerable<StairsLanding> landings = GetLandingsElements();
            stairElements.AddRange(runs);
            stairElements.AddRange(landings);
            List<Face> stairFaces = stairElements.SelectMany(GetStairElementFaces).ToList();
            IEnumerable<Edge> stairEdges = stairElements.SelectMany(GetStairElementEdges);
            List<Curve> stairPath = runs.SelectMany(run => run.GetStairsPath() as IEnumerable<Curve>).ToList();
            stairPath.AddRange(landings.SelectMany(landing => landing.GetStairsPath() as IEnumerable<Curve>));
            IEnumerable<Face> facesIntersectedPath = stairEdges
                .Where(currentEdge =>
                {
                    var curve = currentEdge.AsCurve(); 
                    if (curve.GetEndPoint(0).X.IsAlmostEqual(curve.GetEndPoint(1).X)
                        && curve.GetEndPoint(0).Y.IsAlmostEqual(curve.GetEndPoint(1).Y))
                        return false;
                    double levelZ = stairPath.First().GetEndPoint(0).Z;
                    foreach (var pathCurve in stairPath)
                    {
                        if (pathCurve.Intersect(Line
                                .CreateBound(
                                    new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, levelZ),
                                    new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, levelZ)
                                )
                            ) == SetComparisonResult.Overlap
                        ) { return true; }
                    }
                    return false;
                })
                .SelectMany(e => new List<Face>() { e.GetFace(0), e.GetFace(1) })
                .Distinct();
            stairFaces.RemoveAll(f => facesIntersectedPath.Contains(f));
            sideFacesWithAndWithoutFinishing = stairFaces
                .Where(face => face.ComputeNormal(new UV(0.5, 0.5)).Z.IsAlmostEqual(0.0))
                .GroupBy(face => 
                {
                    if (room == null)
                        return true;
                    else
                        return room.IsPointInRoom(GetPointOnFace(face) + (face.ComputeNormal(new UV(0.5, 0.5)) / 6));
                });

            return sideFacesWithAndWithoutFinishing
                .Where(g => g.Key)
                .SelectMany(g => g)
                .Select(f => f.Area)
                .Sum();
        }

        private double GetSkirtingsArea()
        {
            /**
             * Получает пристеночные грани из списка, собранного в GetSideFacesArea()
             * Получает ребра
             * Вертикальные ребра сравнивает с высотой подъема
             * Горизонтальные ребра берет те, которые находятся на верху грани
             * Возвращает сумму длин ребер умноженную на высоту плинтуса
             */
            
            IEnumerable<Curve> allLines = sideFacesWithAndWithoutFinishing
                .Where(g => !g.Key)
                .SelectMany(group => group)
                .SelectMany(face => face.GetEdgesAsCurveLoops())
                .SelectMany(curveLoop => curveLoop)
                .Select(curve => curve); // Приходят null потому что арки в гранях
            double riserLinesLength = allLines
                .Where(curve => curve is Line)
                .Where(l => Math.Abs((l as Line).Direction.Z) == 1 
                    && l.Length.IsAlmostEqual(riserHeight))
                .Aggregate(0.0, (acc, line) => acc + line.Length);

            double horisontalLinesLength = sideFacesWithAndWithoutFinishing
                .Where(g => !g.Key)
                .SelectMany(g => g)
                .Aggregate(0.0, (acc, face) =>
                    acc + face
                        .GetEdgesAsCurveLoops()
                        .SelectMany(curveLoop => curveLoop)
                        .Where(curve => curve.GetEndPoint(0).Z.IsAlmostEqual(curve.GetEndPoint(1).Z))
                        .OrderByDescending(curve => curve.GetEndPoint(0).Z)
                        .First()
                        .Length
                );
            double skirtingHeight = stair.FindParameter(new Guid("b5921ace-4254-4c03-98da-c868a3b6158b")).AsDouble();
            return (riserLinesLength + horisontalLinesLength) * skirtingHeight;
        }

        private string? GetRoomNumber()
        {
            return room?
                // Берет значение параметра ПО_№_Помещения
                .FindParameter(new Guid("e0c30580-14c7-4cb0-8be2-444179eb94c9"))
                .AsString();
        }

        private string? GetRoomName()
        {
            return room?
                .FindParameter(BuiltInParameter.ROOM_NAME) // Берет значение параметра Имя
                .AsString();
        }

        private IEnumerable<StairsRun> GetRunsElements()
        {
            return stair
                .GetStairsRuns()
                .Select(id => receiveFinishingArea.Document.GetElement(id).Cast<StairsRun>());
        }

        private IEnumerable<StairsLanding> GetLandingsElements()
        {
            return stair
                .GetStairsLandings()
                .Select(id => receiveFinishingArea.Document.GetElement(id).Cast<StairsLanding>());
        }

        private List<Face> GetStairElementFinishingFaces(Element stairElement)
        {
            List<Face> result = [];
            foreach (FaceArray faces in stairElement
                .get_Geometry(new Options() { DetailLevel = ViewDetailLevel.Coarse })
                .ToList()
                .Where(gItem => gItem is GeometryInstance)
                .SelectMany(gi => (gi as GeometryInstance)
                    .GetInstanceGeometry()
                    .ToList()
                    .Where(gItem => gItem is Solid)
                    .Select(gItem => (gItem as Solid).Faces)))
            {
                foreach (object face in faces)
                {
                    if (face is Face)
                        result.Add(face as Face);
                }
            }
            return result;
        }

        private List<Face> GetStairElementFaces(Element stairElement)
        {
            List<Face> result = [];
            foreach (FaceArray faces in stairElement
                .get_Geometry(new Options() { DetailLevel = ViewDetailLevel.Coarse })
                .ToList()
                .Where(gItem => gItem is Solid)
                .Select(gItem => (gItem as Solid).Faces))
            {
                foreach (object face in faces)
                {
                    if (face is Face)
                        result.Add(face as Face);
                }
            }
            return result;
        }

        private List<Edge> GetStairElementEdges(Element stairElement)
        {
            List<Edge> result = [];
            foreach (EdgeArray edges in stairElement
                .get_Geometry(new Options() { DetailLevel = ViewDetailLevel.Coarse })
                .ToList()
                .Where(gItem => gItem is Solid)
                .Select(gItem => (gItem as Solid).Edges))
            {
                foreach (object edge in edges)
                {
                    if (edge is Edge)
                        result.Add(edge as Edge);
                }
            }
            return result;
        }

        private Room? GetRoom()
        {
            StairsRun firstRun = stair.GetStairsRuns()
                .Select(id => receiveFinishingArea.Document.GetElement(id).Cast<StairsRun>())
                .First();
            XYZ firstRunCentroid = (firstRun
                .get_Geometry(new Options() { DetailLevel = ViewDetailLevel.Coarse })
                .First(item => item is Solid) as Solid)
                .ComputeCentroid();
            return receiveFinishingArea.allRooms.FirstOrDefault(room => room.IsPointInRoom(firstRunCentroid));
        }

        private XYZ GetPointOnFace(Face face)
        {
            return face.GetEdgesAsCurveLoops()
                .SelectMany(curveLoop => curveLoop)
                .OrderByDescending(curve => curve.GetEndPoint(0).Z + curve.GetEndPoint(1).Z)
                .First()
                .Evaluate(0.5, false);
        }
    }

}
