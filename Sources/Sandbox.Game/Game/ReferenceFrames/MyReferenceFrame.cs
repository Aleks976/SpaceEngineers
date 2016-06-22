using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using VRage.Utils;
using VRageMath;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Models;
using VRage.Game.Gui;
using VRage.Game.Utils;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.Game.Entity;
using Sandbox.Engine.Utils;
using Sandbox.Game.World;

namespace Sandbox.Game.ReferenceFrames
{
    public class MyReferenceFrame
    {
        public Vector3 globalPosition = new Vector3();
        public Vector3 globalLinearVelocity = new Vector3();
        public HashSet<MyEntity> Entities = new HashSet<MyEntity>();
        public MyEntity mainObject;
        private double bounds_radius;


        public void UpdateBeforePhysicsSimulation()
        {
            foreach(MyEntity entity in Entities)
            {
                if (entity == null)
                {
                    Entities.Remove(entity);
                }
                if (Vector3.Distance(entity.PositionComp.GetPosition(),globalPosition) > bounds_radius) //If entity leaves the reference frame, give it it's own frame
                {
                    RemoveEntityFromFrame(entity);
                    MyReferenceFrame newFrame = new MyReferenceFrame();
                    newFrame.AddEntityToFrame(entity);
                    MySession.Static.referenceFrames.Add(newFrame);
                    if (entity == mainObject)
                    {
                        
                    }
                }
            }
        }

        Vector3 distanceMoved = new Vector3();

        public void UpdateAfterPhysicsSimulation()
        {
            distanceMoved = globalLinearVelocity * VRage.Game.MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * MyFakes.SIMULATION_SPEED;
            globalPosition += distanceMoved;
            foreach (MyEntity entity in Entities)
            {
                if (entity == null)
                {
                    Entities.Remove(entity);
                }
                entity.PositionComp.SetPosition(entity.PositionComp.GetPosition() + distanceMoved);
            }
        }

        private void RemoveEntityFromFrame(MyEntity entityToRemove)
        {
            Entities.Remove(entityToRemove);
            entityToRemove.Physics.LinearVelocity += globalLinearVelocity;
        }

        public void AddEntityToFrame(MyEntity entityToAdd)
        {
            if (Entities.Count == 0)
            {
                mainObject = entityToAdd;
            }
            entityToAdd.Physics.LinearVelocity -= globalLinearVelocity;
        }

    }
}
