using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using VRageMath;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;

namespace Sandbox.Game.Screens.Helpers
{
    public class MyToolbarSubItem
    {
        public int CurrentItemIndex;
        public MyToolbarItem[] ToolbarItems;
        public MyToolbarItem CurrentItem;

        public MyToolbarSubItem(MyGuiBlockCategoryDefinition category, MyToolbarItem toolBarItem = null)
        {
            CurrentItemIndex = 0;
            if (category == null)
            {
                if (toolBarItem != null)
                {
                    ToolbarItems = new MyToolbarItem[1];
                    ToolbarItems[CurrentItemIndex] = toolBarItem;
                    CurrentItem = ToolbarItems[CurrentItemIndex];
                    //Assign toolbarItem values
                }
                return;
            }
            else
            {
                ToolbarItems = new MyToolbarItem[category.ItemIds.Count];
                int inc = 0;

                MyDefinitionBase outblock;
                foreach (string itemID in category.ItemIds)
                {
                    var split = itemID.Split('/');
                    MyObjectBuilderType type = new MyObjectBuilderType();
                    if (MyObjectBuilderType.TryParse("MyObjectBuilder_" + split[0], out type))
                    {
                        MyDefinitionId blockid = new MyDefinitionId(type, split[1]);
                        if (MyDefinitionManager.Static.TryGetDefinition(blockid, out outblock))
                        {
                            var toolbarObjectBuilder = MyToolbarItemFactory.ObjectBuilderFromDefinition(outblock);
                            ToolbarItems[inc] = MyToolbarItemFactory.CreateToolbarItem(toolbarObjectBuilder);
                        }
                        inc++;
                    }
                }
                CurrentItem = ToolbarItems[CurrentItemIndex];
            }
        }

        public virtual void OnClose() { }

        public void GoToNextItem()
        {
            int loopCount = 0;
            int CurrentSlot = MyToolbarComponent.CurrentToolbar.SelectedSlot.Value + 9 * MyToolbarComponent.CurrentToolbar.CurrentPage;
            do
            {
                CurrentItemIndex++;
                if (ToolbarItems != null)
                {
                    if (ToolbarItems.Length == 1)
                    {
                        return;
                    }
                    if (CurrentItemIndex > ToolbarItems.Length - 1)
                    {
                        CurrentItemIndex = 0;
                    }
                    CurrentItem = ToolbarItems[CurrentItemIndex];
                }
                loopCount++;
            } while (CurrentItem == null && loopCount < ToolbarItems.Length + 1);
            MyToolbarComponent.CurrentToolbar.SetItemAtIndex(MyToolbarComponent.CurrentToolbar.SelectedSlot.Value, CurrentItem,true);
            MyToolbarComponent.CurrentToolbar.ActivateItemAtIndex(CurrentSlot,true);
        }

        public void GoToPreviousItem()
        {
            int loopCount = 0;
            int CurrentSlot = MyToolbarComponent.CurrentToolbar.SelectedSlot.Value + 9 * MyToolbarComponent.CurrentToolbar.CurrentPage;
            do
            {
                CurrentItemIndex--;
                if (ToolbarItems != null)
                {
                    if (ToolbarItems.Length == 1)
                    {
                        return;
                    }
                    if (CurrentItemIndex < 0)
                    {
                        CurrentItemIndex = ToolbarItems.Length - 1;
                    }
                    CurrentItem = ToolbarItems[CurrentItemIndex];
                }
                loopCount++;
            } while (CurrentItem == null && loopCount < ToolbarItems.Length + 1);
            MyToolbarComponent.CurrentToolbar.SetItemAtIndex(MyToolbarComponent.CurrentToolbar.SelectedSlot.Value, CurrentItem,true);
            MyToolbarComponent.CurrentToolbar.ActivateItemAtIndex(CurrentSlot,true);
        }

    }
}
