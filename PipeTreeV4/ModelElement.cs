using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB;
using System.Security.Policy;

namespace PipeTreeV4
{
    public class ModelElement
    {
        public ElementId ModelElementId { get; set; }

        public string ModelVolume { get; set; }
        public double  Volume { get; set; }
        public double ModelDiameter { get; set; }
        public double Diameter { get; set; }
        public double ModelLength { get; set; }

        public string ModelTrack { get; set; }
        public string ModelLvl { get; set; }
        public int ModelTrackNumber { get; set; }
        public int ModelBranchNumber { get; set; }
        public string ModelName { get; set; }

        public string Type { get; set; }

        public static double ConvertToDouble(string input)
        {
            // Проверяем, содержится ли в строке символ "ø"
            if (input.Contains("ø"))
            {
                // Разделяем строку по символу '-'
                string[] parts = input.Split('-');

                // Берем первый элемент и удаляем "ø" и " мм"
                string firstPart = parts[0].Replace("ø", "").Replace(" мм", "").Trim();

                // Пробуем преобразовать в double
                if (double.TryParse(firstPart, out double result))
                {
                    return result;
                }
            }

            // Если преобразование не удалось, возвращаем 0 или можно обработать ошибку по-другому
            return 0;
        }

        public ModelElement(Autodesk.Revit.DB.Document document,Node node, int branchcounter, int counter)
        {
            ModelElementId = node.ElementId;

            Element modelelement = document.GetElement(ModelElementId);
            if (modelelement is Pipe || modelelement is FamilyInstance)
            {
                if (node.IsOCK==true)

                {

                    Type = "ОЦК";

                }
                else
                {
                    Type = "ВЦК";
                }
               /* if (modelelement.LookupParameter("Старт_расчета") != null && modelelement.LookupParameter("Старт_расчета").AsString() == "1")
                {

                    Type = "ОЦК";

                }
                else
                {
                    Type = "ВЦК";
                }*/



                if (document.GetElement(ModelElementId).LookupParameter("Длина") != null)
                {
                    ModelLength = document.GetElement(ModelElementId).LookupParameter("Длина").AsDouble() * 304.8;
                }
                else
                {
                    ModelLength = 0;
                }
                //modelElement2.ModelLength = document.GetElement(elId).LookupParameter("Длина").AsDouble() * 304.8;
                ModelName = document.GetElement(ModelElementId).Name;
                if (document.GetElement(ModelElementId).LookupParameter("Базовый уровень") != null)
                {
                    ModelLvl = document.GetElement(ModelElementId).LookupParameter("Базовый уровень").AsValueString();
                }

                else
                {
                    ModelLvl = document.GetElement(ModelElementId).LookupParameter("Уровень").AsValueString();
                }

                ModelTrack = document.GetElement(ModelElementId).LookupParameter("Имя системы").AsString();


                if (document.GetElement(ModelElementId).LookupParameter("Расход") != null)
                {
                    ModelVolume = document.GetElement(ModelElementId).LookupParameter("Расход").AsValueString();
                    if (node.Connectors.Count != 0)
                    {
                        Volume = node.Connectors.FirstOrDefault().Flow;
                    }
                    else
                    {
                        Volume = document.GetElement(ModelElementId).LookupParameter("Расход").AsDouble();
                    }
                }
                else if (document.GetElement(ModelElementId).LookupParameter("ADSK_Расход жидкости") != null)
                {
                    ModelVolume = document.GetElement(ModelElementId).LookupParameter("ADSK_Расход жидкости").AsValueString();
                    if (node.Connectors.Count != 0)
                    {
                        Volume = node.Connectors.FirstOrDefault().Flow;
                    }
                    else
                    {
                        Volume = document.GetElement(ModelElementId).LookupParameter("ADSK_Расход жидкости").AsDouble();
                    }

                    
                }
                else if (node.IsElbow==true)
                {
                    ModelVolume = "-";
                    if (node.Connectors.Count != 0)
                    {
                        Volume = node.Connectors.FirstOrDefault().Flow;
                    }
                    else
                    {
                        Volume = 0;
                    }
                }
                else if (node.IsTee ==true)
                {
                    ModelVolume = "-";
                    Volume = 0;
                }

                /*else
                {
                    ModelVolume = "-";
                }*/
                if (document.GetElement(ModelElementId).LookupParameter("Диаметр") != null && document.GetElement(ModelElementId).LookupParameter("Диаметр").AsDouble() != 0)
                {
                    ModelDiameter = document.GetElement(ModelElementId).LookupParameter("Диаметр").AsDouble() * 304.8;
                    try
                    {
                        Diameter = node.Connectors.First().Diameter * 304.8;
                    }
                    catch
                    {
                        
                    }
                }
                else if (document.GetElement(ModelElementId).LookupParameter("Условный диаметр") != null && document.GetElement(ModelElementId).LookupParameter("Условный диаметр").AsDouble() != 0)
                {
                    ModelDiameter = document.GetElement(ModelElementId).LookupParameter("Условный диаметр").AsDouble() * 304.8;
                    try
                    {
                        Diameter = node.Connectors.First().Diameter * 304.8;
                    }
                    catch
                    {
                        Diameter = 0;
                    }
                }
                else if (document.GetElement(ModelElementId).LookupParameter("D") != null && document.GetElement(ModelElementId).LookupParameter("D").AsDouble() != 0)
                {
                    ModelDiameter = document.GetElement(ModelElementId).LookupParameter("D").AsDouble() * 304.8;
                    try
                    {
                        Diameter = node.Connectors.First().Diameter * 304.8;
                    }
                    catch
                    {
                        Diameter = 0;
                    }
                    
                }
                else if (document.GetElement(ModelElementId).LookupParameter("Размер") != null)
                {
                    string size = document.GetElement(ModelElementId).LookupParameter("Размер").AsString();
                    ModelDiameter = ConvertToDouble(size);
                    Diameter = ModelDiameter / 304.8;
                }
                else
                {
                   
                    Diameter = 0;
                }

                // ModelBranchNumber = branchcounter;
                ModelBranchNumber = node.BranchNumber;
                ModelTrackNumber = counter;
            }


        }

    }
}
