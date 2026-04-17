using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Match3
{
    [CustomPropertyDrawer(typeof(MatchShape))]
    public class MatchShapePropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            root.style.width = Length.Percent(100);
            root.style.flexDirection = FlexDirection.Row;
        
            VisualElement shapeSection = new VisualElement();
            shapeSection.style.width = Length.Percent(50);

            VisualElement settingSection = new VisualElement();
            settingSection.style.width = Length.Percent(50);

            root.Add(shapeSection);
            root.Add(settingSection);

            VisualElement mirrorSection = new VisualElement()
            {
                style = { flexDirection = FlexDirection.Row }
            };
            mirrorSection.Add(new PropertyField(property.FindPropertyRelative(nameof(MatchShape.CanMirror)), ""));
            mirrorSection.Add(new Label("Can be Mirrored"));
        
            VisualElement rotateSection = new VisualElement()
            {
                style = { flexDirection = FlexDirection.Row }
            };
            rotateSection.Add(new PropertyField(property.FindPropertyRelative(nameof(MatchShape.CanRotate)), ""));
            rotateSection.Add(new Label("Can be Rotated"));
        
            settingSection.Add(mirrorSection);
            settingSection.Add(rotateSection);

            CreateUI(property, shapeSection);
        
            return root;
        }

        int FindHeight(SerializedProperty property)
        {
            int yMin = int.MaxValue;
            int yMax = int.MinValue;

            for (int i = 0; i < property.arraySize; ++i)
            {
                var y = property.GetArrayElementAtIndex(i).vector3IntValue.y;

                if (y < yMin) yMin = y;
                else if (y > yMax) yMax = y;
            }

            return yMax - yMin + 1;
        }

        void CreateUI(SerializedProperty property, VisualElement root)
        {
            root.Clear();
        
            //need to rebuild the list as we only have ac cess to serializedProperty and easier to work with an array lower
            var cells = property.FindPropertyRelative(nameof(MatchShape.Cells));
            List<Vector3Int> rebuiltCells = new();

            for (int i = 0; i < cells.arraySize; ++i)
            {
                rebuiltCells.Add(cells.GetArrayElementAtIndex(i).vector3IntValue);
            }
        
            var bound = MatchShape.GetBoundOf(rebuiltCells);

            for (int y = bound.height + 1; y >= -1; --y)
            {
                var line = new VisualElement();
                line.name = "Line" + y;
                line.style.width = Length.Percent(100);
                line.style.height = 18;
                line.style.flexDirection = FlexDirection.Row;
                root.Add(line);
            
                for (int x = bound.x - 1; x <= bound.width + 2; ++x)
                {
                    var realPos = new Vector3Int(x, y + bound.yMin, 0);

                    VisualElement newElem = null;
                
                    if (rebuiltCells.Contains(realPos))
                    {
                        //this is a cell
                        var l = new Label
                        {
                            text = "-"
                        };

                        l.style.backgroundColor = Color.black;
                    
                        l.RegisterCallback<ClickEvent>(evt =>
                        {
                            RemoveCell(property, rebuiltCells.IndexOf(realPos));
                            CreateUI(property, root);
                        });
                    
                        newElem = l;
                    }
                    else if (rebuiltCells.Contains(realPos + Vector3Int.right) ||
                             rebuiltCells.Contains(realPos + Vector3Int.down) ||
                             rebuiltCells.Contains(realPos + Vector3Int.left) ||
                             rebuiltCells.Contains(realPos + Vector3Int.up))
                    {
                        //not a cell but cell on the right
                        var l = new Label
                        {
                            text = "+"
                        };
                    
                        l.RegisterCallback<ClickEvent>(evt =>
                        {
                            AddNewCell(property, realPos);
                            CreateUI(property, root);
                        });
                    
                        newElem = l;
                    }
                    else
                    {
                        //not a cell and no cell adjacent
                        var l = new Label
                        {
                            text = " "
                        };

                        newElem = l;
                    }

                    newElem.style.unityTextAlign = TextAnchor.MiddleCenter;
                    newElem.style.width = 18;
                    line.Add(newElem);
                }
            }
        }

        void RemoveCell(SerializedProperty property, int index)
        {
            property.serializedObject.Update();
        
            var cells = property.FindPropertyRelative(nameof(MatchShape.Cells));
            cells.DeleteArrayElementAtIndex(index);
        
            property.serializedObject.ApplyModifiedProperties();
        }

        void AddNewCell(SerializedProperty property, Vector3Int cell)
        { 
            property.serializedObject.Update();
        
            var cells = property.FindPropertyRelative(nameof(MatchShape.Cells));
       
            cells.InsertArrayElementAtIndex(cells.arraySize);
            cells.GetArrayElementAtIndex(cells.arraySize-1).vector3IntValue = cell;

            property.serializedObject.ApplyModifiedProperties();
        }
    }
}