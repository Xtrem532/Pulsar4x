

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Pulsar4X.ECSLib;

namespace Pulsar4X.SDL2UI
{
    public class ResearchWindow : PulsarGuiWindow
    {
        private FactionTechDB _factionTechDB;
        private Dictionary<Guid, (TechSD tech, int amountDone, int amountMax)> _researchableTechsByGuid;
        private List<(TechSD tech, int amountDone, int amountMax)> _researchableTechs;
        
        private EntityState _currentEntity;
        private TeamsHousedDB _teamsHousedDB;
        private List<(Scientist scientist, Entity atEntity)> _scienceTeams;
        private int _selectedTeam = -1;
       
        private ResearchWindow()
        {
            OnFactionChange();
            _state.Game.GameLoop.GameGlobalDateChangedEvent += GameLoopOnGameGlobalDateChangedEvent; 
        }

        private void GameLoopOnGameGlobalDateChangedEvent(DateTime newdate)
        {
            if (IsActive)
            {
                _researchableTechs = _factionTechDB.GetResearchableTechs();
                _researchableTechsByGuid = _factionTechDB.GetResearchablesDic();
            }
        }


        internal static ResearchWindow GetInstance()
        {
            ResearchWindow thisitem;
            if (!_state.LoadedWindows.ContainsKey(typeof(ResearchWindow)))
            {
                thisitem = new ResearchWindow();
            }
            thisitem = (ResearchWindow)_state.LoadedWindows[typeof(ResearchWindow)];
            if (_state.LastClickedEntity != thisitem._currentEntity)
            {
                if (_state.LastClickedEntity.Entity.HasDataBlob<TeamsHousedDB>())
                {
                    thisitem.OnEntityChange(_state.LastClickedEntity);
                }
            }


            return thisitem;
        }


        private void OnFactionChange()
        {
            _factionTechDB = _state.Faction.GetDataBlob<FactionTechDB>();
            _researchableTechs = _factionTechDB.GetResearchableTechs();
            _researchableTechsByGuid = _factionTechDB.GetResearchablesDic();
            _scienceTeams = _factionTechDB.AllScientists;
        }

 

        private void OnEntityChange(EntityState entityState)
        {
            _currentEntity = entityState;

            _teamsHousedDB = (TeamsHousedDB)entityState.DataBlobs[typeof(TeamsHousedDB)];
        }


        internal override void Display()
        {
            if (IsActive && ImGui.Begin("Research and Development", ref IsActive, _flags))
            {
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 300);
                ImGui.Text("Projects");
                ImGui.NextColumn();
                ImGui.Text("Science Teams");
                ImGui.NextColumn();
                ImGui.Separator();
                
                ImGui.BeginChild("ResearchablesHeader", new Vector2(300, ImGui.GetTextLineHeightWithSpacing() + 2));
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 250);
                ImGui.Text("Tech");
                ImGui.NextColumn();
                ImGui.Text("Level");
                ImGui.NextColumn();
                ImGui.Separator();
                ImGui.EndChild();
                
                ImGui.BeginChild("techlist", new Vector2(300, 250));
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0,250);
                
                for (int i = 0; i < _researchableTechs.Count; i++)
                {
                    if (_researchableTechs[i].amountMax > 0) //could happen if bad json data?
                    {
                        float frac = (float)_researchableTechs[i].amountDone / _researchableTechs[i].amountMax;
                        var size = ImGui.GetTextLineHeight();
                        var pos = ImGui.GetCursorPos();
                        ImGui.ProgressBar(frac, new Vector2(248, size), "");
                        ImGui.SetCursorPos(pos);
                        ImGui.Text(_researchableTechs[i].tech.Name);
                        
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
                        {
                            if(_selectedTeam > -1)
                                ResearchProcessor.AssignProject(_scienceTeams[_selectedTeam].scientist, _researchableTechs[i].tech.ID);
                        }
                        if(ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(_researchableTechs[i].tech.Description);
                        }
                        ImGui.NextColumn();
                        ImGui.Text(_factionTechDB.LevelforTech(_researchableTechs[i].tech).ToString());
                        
                        ImGui.NextColumn();
                    }
                }
                ImGui.EndChild();
                
                ImGui.NextColumn();

                ImGui.BeginChild("Teams", new Vector2(550, 250));
                
                ImGui.Columns(4);
                ImGui.SetColumnWidth(0, 150);
                ImGui.SetColumnWidth(1, 150);
                ImGui.SetColumnWidth(2, 100);
                ImGui.SetColumnWidth(3, 150);
                ImGui.Text("Scientist");
                ImGui.NextColumn();
                ImGui.Text("Location");
                ImGui.NextColumn();
                ImGui.Text("Labs");
                ImGui.NextColumn();
                ImGui.Text("Current Project");
                ImGui.NextColumn();
                ImGui.Separator();
                for (int i = 0; i < _scienceTeams.Count; i++)
                {

                    bool isSelected = _selectedTeam == i;
                    
                    Scientist scint = _scienceTeams[i].scientist;
                    if (ImGui.Selectable(_scienceTeams[i].Item1.Name, isSelected))
                    {
                        _selectedTeam = i;
                    }

                    ImGui.NextColumn();
                    ImGui.Text(_scienceTeams[i].atEntity.GetDataBlob<NameDB>().GetName(_state.Faction));
                    
                    ImGui.NextColumn();
                    int allfacs = 0;
                    int facsAssigned = scint.AssignedLabs;
                    //int facsFree = 0;
                    if(
                    _scienceTeams[i].atEntity.GetDataBlob<ComponentInstancesDB>().TryGetComponentsByAttribute<ResearchPointsAtbDB>( out var foo ))
                    {
                        allfacs = foo.Count;
                        //facsFree = allfacs - facsAssigned;
                    }
                    ImGui.Text(facsAssigned.ToString() + "/" + allfacs.ToString());
                    if(ImGui.IsItemHovered())
                        ImGui.SetTooltip("Assigned / Total");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("+"))
                    {
                        ResearchProcessor.AddLabs(scint, 1);
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("-"))
                    {
                        ResearchProcessor.AddLabs(scint, -1);
                    }

                    ImGui.NextColumn();
                    if (scint.ProjectQueue.Count > 0 && _factionTechDB.IsResearchable(scint.ProjectQueue[0].techID))
                    {
                        var proj = _researchableTechsByGuid[scint.ProjectQueue[0].techID];
                        
                        float frac = (float)proj.amountDone / proj.amountMax;
                        var size = ImGui.GetTextLineHeight();
                        var pos = ImGui.GetCursorPos();
                        ImGui.ProgressBar(frac, new Vector2(150, size), "");
                        ImGui.SetCursorPos(pos);
                        ImGui.Text(proj.tech.Name);
                        if(ImGui.IsItemHovered())
                        {
                            string queue = "";
                            foreach (var queueItem in _scienceTeams[i].scientist.ProjectQueue)
                            {
                                queue += _researchableTechsByGuid[queueItem.techID].tech.Name + "\n";
                            }
                            ImGui.SetTooltip(queue);
                        }



                    }
                }
                ImGui.EndChild();
                ImGui.Separator();
                ImGui.Columns(1);
                if (_selectedTeam > -1)
                {
                    SelectedSci(_selectedTeam);
                }
            }
        }

        private void SelectedSci(int selected)
        {
            ImGui.BeginChild("SelectedSci");
            Scientist scientist = _scienceTeams[selected].scientist;
            bool isDirty = false;
            ImGui.Columns(2);
            ImGui.SetColumnWidth(0, 150);
            for (int i = 0; i < scientist.ProjectQueue.Count; i++)
            {
                
                (Guid techID, bool cycle) queueItem = _scienceTeams[selected].scientist.ProjectQueue[i];
                (TechSD tech, int amountDone, int amountMax) projItem = _researchableTechsByGuid[queueItem.techID];
                ImGui.Text(projItem.tech.Name);
                //ImGui.Text(proj.Description);
                ImGui.NextColumn();

                string cyclestr = "*";
                if (queueItem.cycle)
                    cyclestr = "O";
                if (ImGui.SmallButton(cyclestr + "##" + i))
                {
                    scientist.ProjectQueue[i] = (queueItem.techID, !queueItem.cycle);
                }
                //if(ImGui.IsItemHovered())
                //    ImGui.SetTooltip("Requeue Project");
                
                ImGui.SameLine();
                if (ImGui.SmallButton("^" + "##" + i) )//&& i > 0)
                {
                    scientist.ProjectQueue.RemoveAt(i);
                    scientist.ProjectQueue.Insert(i-1, queueItem);
                    //isDirty = true;
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("v" + "##" + i) )//&& i < scientist.ProjectQueue.Count)
                {
                    
                    scientist.ProjectQueue.RemoveAt(i);
                    scientist.ProjectQueue.Insert(i+1, queueItem);
                    //isDirty = true;
                }

                ImGui.SameLine();
                if (ImGui.SmallButton("x" + "##" + i))
                {
                    scientist.ProjectQueue.RemoveAt(i);
                    //isDirty = true;
                }



                ImGui.NextColumn();
            }

            if (isDirty)
            {
                _researchableTechs = _factionTechDB.GetResearchableTechs();
                _researchableTechsByGuid = _factionTechDB.GetResearchablesDic();
            }
            
            ImGui.EndChild();

        }
        
    }
}