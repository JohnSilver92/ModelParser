using System;
using System.IO;
using System.Linq;
using Geometry;
using Model.Interfaces;
using System.Globalization;
using System.Collections.Generic;
using Model;
using Model.GroupsData;
using Model.Events;

namespace ModelParcer
{
    public class LoadModelFromGMSHTextFile : IModelLoader
    {
        public event Action<object, LoaderEventArgs> LoadEvent;

        public ModelData Load(string path)
        {
            var dic = new Dictionary<int, int>();
            var sr = new StreamReader(path);
            var newMesh = new ModelData();
            var line = sr.ReadLine();
            var nodeList = new List<Node>();

            LoadEvent(this, new LoaderEventArgs("Начало загрузки модели"));
            while (!sr.EndOfStream)
            {
                line = sr.ReadLine();
                if (line.Contains("*NODE"))
                {
                    line = sr.ReadLine();
                    LoadEvent(this, new LoaderEventArgs("Начало загрузки узлов"));
                    while (line != "******* E L E M E N T S *************")
                    {
                        var strAr = line.Replace(" ", "").Split(',').Select(x => float.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();

                        nodeList.Add(new Node((int)strAr[0] - 1, new Point3D(strAr[1], strAr[2], strAr[3])));
                        line = sr.ReadLine();
                    }
                    var orderedNodes = nodeList.OrderBy(x => x.Number);
                    newMesh.ObjectData.AddRange(orderedNodes);
                }

                if (line.Contains("type=T3D2"))
                {
                    LoadEvent(this, new LoaderEventArgs("Начало загрузки элементов"));
                    line = sr.ReadLine();

                    CreateElements("type=T3D2", newMesh, sr, line, "*ELSET", dic);
                }
            }
            return newMesh;
        }

        public void CreateElements(string elkind, ModelData newMesh, StreamReader sr, string line, string recBase, Dictionary<int, int> dic)
        {
            var nodes = newMesh.ObjectData.FindMany<Node>().ToList();
            var strAr = line.Replace(" ", "").Split(',');
            string[] checkArr = new string[3];
            checkArr[0] = "*ELEMENT";
            checkArr[1] = "*ELSET";
            checkArr[2] = "*NSET";

            var stopWord = checkArr.Where(x => x == strAr[0]).ToList();
            
            while (line != null && stopWord.Count == 0)
            {
                int currentNumber = newMesh.ObjectData.GetLastObjNumber() + 1;
                dic.Add(int.Parse(strAr[0]), currentNumber);

                var intAr = strAr.Select(m => int.Parse(m) - 1).ToList();

                if (elkind == "type=T3D2") // 1D
                {
                    var nodeArr = new Node[2];
                    nodeArr[0] = newMesh.ObjectData.Find(nodes, intAr[1]);
                    nodeArr[1] = newMesh.ObjectData.Find(nodes, intAr[2]);

                    newMesh.ObjectData.Add(new Element1D(currentNumber, nodeArr, ObjKind.Beam));
                }

                else if (elkind == "type=CPS3") // 2D
                {
                    var nodeArr = new Node[3];
                    nodeArr[0] = newMesh.ObjectData.Find(nodes, intAr[1]);
                    nodeArr[1] = newMesh.ObjectData.Find(nodes, intAr[2]);
                    nodeArr[2] = newMesh.ObjectData.Find(nodes, intAr[3]);
                    newMesh.ObjectData.Add(new Element2D(currentNumber, nodeArr, ObjKind.Triangle));
                }

                else if (elkind == "type=C3D4") // 3D
                {
                    var nodeArr = new Node[4];
                    nodeArr[0] = newMesh.ObjectData.Find(nodes, intAr[1]);
                    nodeArr[1] = newMesh.ObjectData.Find(nodes, intAr[2]);
                    nodeArr[2] = newMesh.ObjectData.Find(nodes, intAr[3]);
                    nodeArr[3] = newMesh.ObjectData.Find(nodes, intAr[4]);
                    newMesh.ObjectData.Add(new Element3D(currentNumber, nodeArr, ObjKind.Tetra));
                }

                line = sr.ReadLine();
                if (line != null)
                {
                    strAr = line.Replace(" ", "").Split(',');
                    stopWord = checkArr.Where(x => x == strAr[0]).ToList();
                }   
            }

            if (line != null && strAr[0] != recBase)
            {
                strAr = line.Replace(" ", "").Split(',');
                var tempKind = strAr[1];
                line = sr.ReadLine();
                CreateElements(tempKind, newMesh, sr, line, recBase, dic);
            }

            if (line != null && line.Contains("*ELSET"))
            {
                CreateGroups(line, newMesh, sr, dic);
            }
        }

        public void CreateGroups(string line, ModelData newMesh, StreamReader sr, Dictionary<int, int> dic)
        {
            var grName = line.Split('=').Last();
            var strAr = line.Split(',');

            while (!sr.EndOfStream || line.Contains("*NSET"))
            {
                line = sr.ReadLine();
                strAr = line.Replace(" ", "").Split(',');
                if (strAr[0] == "")
                    break;

                var firstNumber = int.Parse(strAr.First());
                var objNumber = dic[firstNumber];
                var objsType = newMesh.ObjectData.Find(objNumber).ObjType;

                LoadEvent(this, new LoaderEventArgs($"Загрузка группы объектов {grName}"));
                var group = new Group(grName, objsType);

                //var oldListInt = strAr.Where(x => !string.IsNullOrEmpty(x));
                //var newListInt = oldListInt.Where(x => dic.TryGetValue(int.Parse(x) - 1, out int res)).
                //Select(x => dic[int.Parse(x) - 1]).ToList();

                while (line != null && !line.Contains("*NSET") && !line.Contains("*ELSET"))
                {
                    strAr = line.Replace(" ", "").Split(',');
                    var objsNumbers = strAr.Where(x => !string.IsNullOrEmpty(x)).Select(y =>
                    dic[int.Parse(y)]);

                    group.AddRange(objsNumbers);

                    line = sr.ReadLine();
                }
                newMesh.GroupData.Add(group);
                if (line != null)
                    grName = line.Split('=').Last();
                if (line.Contains("*NSET"))
                    break;
            }
        }
    }
}

