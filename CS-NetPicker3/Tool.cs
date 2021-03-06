﻿using System;
using System.Collections.Generic;
using UnityEngine;
using ColossalFramework.UI;
using System.Reflection;
using ColossalFramework.Globalization;

namespace CS_NetPicker3
{
    public static class Db
    {
        public static bool on = false;

        public static void l(object m)
        {
            if (on) Debug.Log(m);
        }

        public static void w(object m)
        {
            if (on) Debug.LogWarning(m);
        }

        public static void e(object m)
        {
            if (on) Debug.LogWarning(m);
        }

        // Extensions
        public static NetSegment S(this ushort s)
        {
            return NetManager.instance.m_segments.m_buffer[s];
        }

        public static NetNode N(this ushort n)
        {
            Debug.Log(n);
            return NetManager.instance.m_nodes.m_buffer[n];
        }
        public static PropInstance P(this ushort p)
        {
            return PropManager.instance.m_props.m_buffer[p];
        }

        public static Building B(this ushort b)
        {
            return BuildingManager.instance.m_buildings.m_buffer[b];
        }

        public static TreeInstance T(this uint t)
        {
            return TreeManager.instance.m_trees.m_buffer[t];
        }

        public static Vector3 Position(this InstanceID id)
        {
            
            if (id.NetSegment != 0) return id.NetSegment.S().m_middlePosition;
            if (id.NetNode != 0 && id.NetNode < 32768) return id.NetNode.N().m_position;
            if (id.Prop != 0) return id.Prop.P().Position;
            if (id.Building != 0) return id.Building.B().m_position;
            if (id.Tree != 0) return id.Tree.T().Position;

            return Vector3.zero;
        }

        public static PrefabInfo Info(this InstanceID id)
        {
            if (id.NetSegment != 0) return id.NetSegment.S().Info;
            if (id.NetNode != 0 && id.NetNode < 32768) return id.NetNode.N().Info;
            if (id.Prop != 0) return id.Prop.P().Info;
            if (id.Building != 0) return id.Building.B().Info;
            if (id.Tree != 0) return id.Tree.T().Info;

            return null;
        }
    }

    public class NetPickerTool : ToolBase
    {
        public static NetPickerTool instance;

        // Allow....?
        public bool np_allowSegments = true;
        public bool np_allowNodes = true;
        public bool np_allowProps = true;
        public bool np_allowTrees = true;
        public bool np_allowBuildings = true;

        public InstanceID np_hoveredObject = InstanceID.Empty;

        public bool np_hasSteppedOver = false;
        public List<InstanceID> np_stepOverBuffer = new List<InstanceID>();
        public int np_stepOverCounter = 0;
        public Vector3 np_stepOverPosition = Vector3.zero;
        public Vector3 np_mouseCurrentPosition = Vector3.zero;

        private Dictionary<string, UIComponent> _componentCache = new Dictionary<string, UIComponent>();

        private Color _hovc = new Color32(0, 181, 255, 255);

        private T FindComponentCached<T>(string name) where T : UIComponent
        {
            if (!_componentCache.TryGetValue(name, out UIComponent component) || component == null)
            {
                component = UIView.Find<UIButton>(name);
                _componentCache[name] = component;
            }

            return component as T;
        }
        
        private bool ReflectIntoFindIt(BuildingInfo info)
        {
            Type FindItType = Type.GetType("FindIt.FindIt, FindIt");
            object FindItInstance = FindItType.GetField("instance").GetValue(null);
            object UIScrollPanel = FindItType.GetField("scrollPanel").GetValue(FindItInstance);
            Type ScrollPanelType = Type.GetType("FindIt.GUI.UIScrollPanel, FindIt");
            Debug.Log(ScrollPanelType);

            // Get all the item data...
            PropertyInfo iData = ScrollPanelType.GetProperty("itemsData");
            object itemsData = iData.GetValue(UIScrollPanel, null);
            object[] itemDataBuffer = itemsData.GetType().GetMethod("ToArray").Invoke(itemsData, null) as object[];
            Debug.Log(itemDataBuffer);

            for (int i = 0; i < itemDataBuffer.Length; i++)
            {
                object itemData = itemDataBuffer[i];

                // Get the actual asset data of this prefab instance in the Find It scrollable panel
                Type UIScrollPanelItemData = itemData.GetType();
                Debug.Log(UIScrollPanelItemData);
                object itemData_currentData_asset = UIScrollPanelItemData.GetField("asset").GetValue(itemData);
                PrefabInfo itemData_currentData_asset_info = itemData_currentData_asset.GetType().GetProperty("prefab").GetValue(itemData_currentData_asset, null) as PrefabInfo;
                //string itemDataName = itemData_currentData_asset.GetType().GetField("name").GetValue(itemData_currentData_asset) as string;

                // Display data at this position. Return.
                if (itemData_currentData_asset_info != null && itemData_currentData_asset_info.name == info.name)
                {
                    Debug.Log("Found data at position " + i + " in Find it ScrollablePanel");
                    ScrollPanelType.GetMethod("DisplayAt").Invoke(UIScrollPanel, new object[] { i });

                    string itemDataName = UIScrollPanelItemData.GetField("name").GetValue(itemData) as string;
                    Debug.Log(itemDataName);
                    UIComponent test = UIScrollPanel as UIComponent;
                    UIButton[] fYou = test.GetComponentsInChildren<UIButton>();
                    foreach (UIButton mhmBaby in fYou)
                    {
                        if (mhmBaby.name == itemDataName)
                        {
                            mhmBaby.SimulateClick();
                            return true;
                        }
                    }
                    break;
                }
            }

            return false;
        }

        private void ShowInPanelResolveGrowables(PrefabInfo pInfo)
        {
            if(!(pInfo is BuildingInfo))
            {
                ShowInPanel(pInfo);
                return;
            }


            BuildingInfo info = pInfo as BuildingInfo;
            //BuildingInfo info = PrefabCollection<BuildingInfo>.FindLoaded("4x3_Beach Hotel3");
            if(info != null &&
                ((info.m_class.m_service == ItemClass.Service.Residential) ||
                (info.m_class.m_service == ItemClass.Service.Industrial) ||
                (info.m_class.m_service == ItemClass.Service.Commercial) ||
                (info.m_class.m_service == ItemClass.Service.Office) ||
                (info.m_class.m_service == ItemClass.Service.Tourism))){
                Debug.LogWarning("Info " + info.name + " is a growable (or RICO).");

                bool Pass1 = false;

                // Try to locate in Find It!
                UIComponent SearchBoxPanel = UIView.Find("UISearchBox");
                if (SearchBoxPanel != null && SearchBoxPanel.isVisible == false)
                {
                    UIButton FIButton = UIView.Find<UIButton>("FindItMainButton");
                    if (FIButton == null) return;
                    FIButton.SimulateClick();
                }
                if (SearchBoxPanel == null) return;

                UIDropDown FilterDropdown = SearchBoxPanel.Find<UIDropDown>("UIDropDown");
                FilterDropdown.selectedValue = "Growable";

                UITextField TextField = SearchBoxPanel.Find<UITextField>("UITextField");
                TextField.text = "";

                UIComponent UIFilterGrowable = SearchBoxPanel.Find("UIFilterGrowable");
                UIFilterGrowable.GetComponentInChildren<UIButton>().SimulateClick();

                // Reflect into the scroll panel, starting with the growable panel:
                if (!ReflectIntoFindIt(info))
                {

                    // And then if that fails, RICO:
                    FilterDropdown.selectedValue = "Rico";
                    if (!ReflectIntoFindIt(info))
                    {

                        // And then if that fails, give up and get a drink
                        Debug.Log("Could not be found in Growable or Rico menus.");
                    }
                }
            }
            else
            {
                Debug.LogWarning("Info " + info.name + " is not a growable.");
                ShowInPanel(pInfo);
            }
        }

        private void ShowInPanel(PrefabInfo info)
        {
            UIButton button = FindComponentCached<UIButton>(info.name);
            if (button != null)
            {
                UIView.Find("TSCloseButton").SimulateClick();
                UITabstrip subMenuTabstrip = null;
                UIScrollablePanel scrollablePanel = null;
                UIComponent current = button, parent = button.parent;
                int subMenuTabstripIndex = -1, menuTabstripIndex = -1;
                while (parent != null)
                {
                    if (current.name == "ScrollablePanel")
                    {
                        subMenuTabstripIndex = parent.zOrder;
                        scrollablePanel = current as UIScrollablePanel;
                    }
                    if (current.name == "GTSContainer")
                    {
                        menuTabstripIndex = parent.zOrder;
                        subMenuTabstrip = parent.Find<UITabstrip>("GroupToolstrip");
                    }
                    current = parent;
                    parent = parent.parent;
                }
                UITabstrip menuTabstrip = current.Find<UITabstrip>("MainToolstrip");
                if (scrollablePanel == null
                || subMenuTabstrip == null
                || menuTabstrip == null
                || menuTabstripIndex == -1
                || subMenuTabstripIndex == -1) return;
                menuTabstrip.selectedIndex = menuTabstripIndex;
                menuTabstrip.ShowTab(menuTabstrip.tabs[menuTabstripIndex].name);
                subMenuTabstrip.selectedIndex = subMenuTabstripIndex;
                subMenuTabstrip.ShowTab(subMenuTabstrip.tabs[subMenuTabstripIndex].name);
                button.SimulateClick();
                scrollablePanel.ScrollIntoView(button);
            }
        }

        public PrefabInfo Default(PrefabInfo resolve)
        {
            if (!(resolve is NetInfo)) return resolve;

            NetInfo info = resolve as NetInfo;
            for (uint i = 0; i < PrefabCollection<NetInfo>.LoadedCount(); i++)
            {
                NetInfo prefab = PrefabCollection<NetInfo>.GetLoaded(i);
                if ((AssetEditorRoadUtils.TryGetBridge(prefab) != null && AssetEditorRoadUtils.TryGetBridge(prefab).name == info.name) ||
                   (AssetEditorRoadUtils.TryGetElevated(prefab) != null && AssetEditorRoadUtils.TryGetElevated(prefab).name == info.name) ||
                   (AssetEditorRoadUtils.TryGetSlope(prefab) != null && AssetEditorRoadUtils.TryGetSlope(prefab).name == info.name) ||
                   (AssetEditorRoadUtils.TryGetTunnel(prefab) != null && AssetEditorRoadUtils.TryGetTunnel(prefab).name == info.name))
                {
                    return prefab;
                }
            }
            return info;
        }

        protected override void OnToolUpdate()
        {
            base.OnToolUpdate();

            // Raycast to all currently "allowed"
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastInput input = new RaycastInput(ray, Camera.main.farClipPlane);

            input.m_ignoreTerrain = false;
            input.m_ignoreSegmentFlags = np_allowSegments ? NetSegment.Flags.Untouchable : NetSegment.Flags.All;
            input.m_ignoreNodeFlags = np_allowNodes ? NetNode.Flags.Untouchable : NetNode.Flags.All;
            input.m_ignorePropFlags = np_allowProps ? PropInstance.Flags.None : PropInstance.Flags.All;
            input.m_ignoreTreeFlags = np_allowTrees ? TreeInstance.Flags.None : TreeInstance.Flags.All;
            input.m_ignoreBuildingFlags = np_allowBuildings ? Building.Flags.Untouchable : Building.Flags.All;

            input.m_ignoreCitizenFlags = CitizenInstance.Flags.All;
            input.m_ignoreVehicleFlags = Vehicle.Flags.Created;
            input.m_ignoreDisasterFlags = DisasterData.Flags.All;
            input.m_ignoreTransportFlags = TransportLine.Flags.All;
            input.m_ignoreParkedVehicleFlags = VehicleParked.Flags.All;
            input.m_ignoreParkFlags = DistrictPark.Flags.All;
            
            RayCast(input, out RaycastOutput output);

            // Set the world mouse position (useful for my implementation of StepOver)
            np_mouseCurrentPosition = output.m_hitPos;

            // Step Over Block.
            if(Input.GetKeyDown(KeyCode.O)) // @TODO allow user to customize this.
            {
                Debug.Log("attempted to step over");
                np_stepOverCounter++;

                if(np_stepOverCounter == 1 && np_hasSteppedOver == false) // Only will be executed the first time we reach 1.
                {
                    // @TODO populate step over buffer
                }

                np_hasSteppedOver = true;

                if (np_stepOverCounter >= np_stepOverBuffer.Count) np_stepOverCounter = 0;
                np_hoveredObject = np_stepOverBuffer[np_stepOverCounter];
            }

            // This code is used when the step over function is not active. It will choose the closest of any given object and set it as the hovered object.
            if (!np_hasSteppedOver)
            {
                np_stepOverBuffer.Clear();

                if (output.m_netSegment   != 0) np_stepOverBuffer.Add(new InstanceID() { NetSegment = output.m_netSegment   });
                if (output.m_netNode      != 0) np_stepOverBuffer.Add(new InstanceID() { NetNode    = output.m_netNode      });
                if (output.m_treeInstance != 0) np_stepOverBuffer.Add(new InstanceID() { Tree       = output.m_treeInstance });
                if (output.m_propInstance != 0) np_stepOverBuffer.Add(new InstanceID() { NetNode    = output.m_propInstance });
                if (output.m_building     != 0) np_stepOverBuffer.Add(new InstanceID() { Building   = output.m_building     });

                np_stepOverBuffer.Sort((a, b) => Vector3.Distance(a.Position(), np_mouseCurrentPosition).CompareTo(Vector3.Distance(b.Position(), np_mouseCurrentPosition)));
                if (np_stepOverBuffer.Count > 0) np_hoveredObject = np_stepOverBuffer[0];
                else np_hoveredObject = InstanceID.Empty;
            }
            else
            {
                // This function resets the step over function.
                if(np_mouseCurrentPosition != np_stepOverPosition)
                {
                    np_hasSteppedOver = false;
                }
            }
            
            // A prefab has been selected. Find it in the UI and enable it.
            if(Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                ShowInPanelResolveGrowables(Default(np_hoveredObject.Info()));
            }

            // Escape key hit = disable the tool
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                enabled = false;
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderOverlay(cameraInfo);
            if (!enabled) return;

            if(np_hoveredObject.NetSegment != 0)
            {
                NetSegment hoveredSegment = np_hoveredObject.NetSegment.S();
                NetTool.RenderOverlay(cameraInfo, ref hoveredSegment, _hovc, new Color(1f, 0f, 0f, 1f));
            }else if(np_hoveredObject.NetNode != 0 && np_hoveredObject.NetNode < 32768)
            {
                NetNode hoveredNode = np_hoveredObject.NetNode.N();
                RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, _hovc, hoveredNode.m_position, Mathf.Max(6f, hoveredNode.Info.m_halfWidth * 2f), -1f, 1280f, false, true);
            }
            else if(np_hoveredObject.Building != 0)
            {
                Building hoveredBuilding = np_hoveredObject.Building.B();
                BuildingTool.RenderOverlay(cameraInfo, ref hoveredBuilding, _hovc, _hovc);
            }
            else if (np_hoveredObject.Tree != 0)
            {
                TreeInstance hoveredTree = np_hoveredObject.Tree.T();
                TreeTool.RenderOverlay(cameraInfo, hoveredTree.Info, hoveredTree.Position, hoveredTree.Info.m_minScale, _hovc);
            }
            else if (np_hoveredObject.Prop != 0)
            {
                PropInstance hoveredTree = np_hoveredObject.Prop.P();
                PropTool.RenderOverlay(cameraInfo, hoveredTree.Info, hoveredTree.Position, hoveredTree.Info.m_minScale, hoveredTree.Angle, _hovc);
            }
        }
    }
}
