using Geometry;
using Model;
using Model.Events;
using Model.GroupsData;
using Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace ModelParcer
{
    public class LoadModelFromASCIITextFile : IModelLoader
    {
        public event Action<object, LoaderEventArgs> LoadEvent;

        public ModelData Load(string path)
        {
            var newMesh = new ModelData();

            var elChangeNumbers = new Dictionary<int, int>();

            var line = string.Empty;
            var sr = new StreamReader(path);

            while (!sr.EndOfStream)
            {
                var lineCollect = sr.ReadLine().Split(new char[] { });

                if (lineCollect[0] == "BEGIN_NODES")
                {
                    LoadEvent(this, new LoaderEventArgs("Загрузка узлов..."));
                    Thread.Sleep(100);
                    var nodes = new List<Node>();
                    while (line.Split(' ')[0] != "END_NODES")
                    {
                        line = sr.ReadLine();
                        var val = 0.0f;
                        var ar = line.Split(' ').Where(m => float.TryParse(m, NumberStyles.Float, CultureInfo.InvariantCulture, out val)).
                            Select(m => float.Parse(m, NumberStyles.Float, CultureInfo.InvariantCulture)).ToList();

                        if (ar.Count != 0)
                        {
                            //nodeInd.Add((int)ar[0] - 1);
                            ar.RemoveRange(1, 5);
                            nodes.Add(new Node((int)ar[0] - 1,new Point3D(ar[1], ar[2], ar[3])));
                        }
                    }
                    var sortedNodes = nodes.OrderBy(x => x.Number);
 
                    newMesh.ObjectData.AddRange(sortedNodes);
                }
                if (lineCollect[0] == "BEGIN_ELEMENTS")
                {
                    LoadEvent(this, new LoaderEventArgs("Загрузка элементов..."));
                    Thread.Sleep(100);

                    var nodes = newMesh.ObjectData.FindMany<Node>().ToArray();

                    while (line.Split(' ')[0] != "END_ELEMENTS")
                    {
                        line = sr.ReadLine();

                        int val = 0;
                        var list = line.Split(' ').Where(m => int.TryParse(m, out val)).
                            Select(m => int.Parse(m)).ToList();
                        if (list.Count > 1)
                        {
                            //var elInd = list[0] - 1;
                            list.RemoveRange(1, 4);
                            var ar = list.Select(m => m - 1).ToArray();
                            var lastNumber = newMesh.ObjectData.GetLastObjNumber();

                            elChangeNumbers.Add(ar[0], lastNumber + 1);

                            if (line.Split(' ')[1] == "3004")
                            {
                                var n0 = newMesh.ObjectData.Find(nodes, ar[1]);
                                var n1 = newMesh.ObjectData.Find(nodes, ar[2]);
                                var n2 = newMesh.ObjectData.Find(nodes, ar[3]);
                                var n3 = newMesh.ObjectData.Find(nodes, ar[4]);

                                var tetra = new Element3D(lastNumber + 1, new Node[] { n0, n1, n2, n3 }, ObjKind.Tetra);
                                newMesh.ObjectData.Add(tetra);
                            }
                            else if (line.Split(' ')[1] == "2003")
                            {
                                var n0 = newMesh.ObjectData.Find(nodes, ar[1]);
                                var n1 = newMesh.ObjectData.Find(nodes, ar[2]);
                                var n2 = newMesh.ObjectData.Find(nodes, ar[3]);
                                var triangle = new Element2D(lastNumber + 1, new Node[] { n0, n1, n2}, ObjKind.Triangle);
                                newMesh.ObjectData.Add(triangle);
                            }
                            //else if (line.Split(' ')[1] == "1002") { beamNodes.Add(ar); }
                            else
                            {
                                var msg = "\n > Неизвестный тип элемента.Модель будет загружена не полностью!";
                                LoadEvent(this, new LoaderEventArgs(msg));
                            }
                        }
                    }
                }
                if (lineCollect[0] == "BEGIN_GROUPS")
                {
                    line = sr.ReadLine();
                    while (line.Split(' ')[0] != "END_GROUPS")
                    {
                        CreateGroup(newMesh, line, elChangeNumbers);
                        line = sr.ReadLine();
                    }
                }
            }
            return newMesh;
        }

        private void CreateGroup(ModelData newMesh, string line, Dictionary<int,int> elChangeNumbers)
        {
            var listAr = line.Split(' ');

            LoadEvent(this, new LoaderEventArgs("Загрузка группы объектов " + line.Split(' ')[1]));

            var newlistStr = new string[listAr.Length - 13];

            Array.Copy(listAr, 13, newlistStr, 0, listAr.Length - 13);
            var newListInt = newlistStr.Where(x => elChangeNumbers.TryGetValue(int.Parse(x) - 1,out int res)).
            Select(x => elChangeNumbers[int.Parse(x) - 1]).ToList();

            var objType = string.Empty;
            if (newListInt.Count != 0)
            {
                var obj = newMesh.ObjectData.Find(newListInt[0]);

                if(obj != null)
                {
                    var group = new Group(listAr[1], obj.ObjType);
                    group.AddRange(newListInt);
                    newMesh.GroupData.Add(group);
                }
            }
            else
                LoadEvent(this, new LoaderEventArgs("\n > Пустая группа объектов " + listAr[1]));
        }
    }
}
