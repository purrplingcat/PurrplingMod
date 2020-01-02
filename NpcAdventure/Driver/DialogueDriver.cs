﻿using System;
using Microsoft.Xna.Framework;
using NpcAdventure.Internal;
using NpcAdventure.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace NpcAdventure.Driver
{
    public class DialogueDriver
    {
        public event EventHandler<DialogueChangedArgs> DialogueChanged;
        public event EventHandler<SpeakerChangedArgs> SpeakerChanged;
        public event EventHandler<DialogueRequestArgs> DialogueRequested;

        public DialogueDriver(IModEvents events)
        {
            events.GameLoop.UpdateTicking += this.Update;
            events.Input.ButtonPressed += this.HandleAction;
        }

        public Dialogue CurrentDialogue { get; private set; }

        public NPC CurrentSpeaker { get; private set; }

        public void RequestDialogue(Farmer who, NPC withWhom, int requestId)
        {
            if (this.DialogueRequested == null)
                return;

            DialogueRequestArgs args = new DialogueRequestArgs()
            {
                Initiator = who,
                WithWhom = withWhom,
                RequestId = requestId,
            };

            this.DialogueRequested(this, args);
        }

        public void DrawDialogue(NPC speaker)
        {
            Game1.drawDialogue(speaker);
        }

        public void DrawDialogue(NPC speaker, string dialogue)
        {
            Game1.drawDialogue(speaker, dialogue);
        }

        private void Update(object sender, UpdateTickingEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            this.WatchDialogue();
        }

        private void HandleAction(object sender, ButtonPressedEventArgs e)
        {
            bool actionButtonPressed = e.Button.IsActionButton() || e.Button.IsUseToolButton();

            // ignore if player hasn't loaded a save yet, player can't move, pressed other button instead of action or event running
            if (!Context.IsWorldReady || !Context.IsPlayerFree || !actionButtonPressed || Game1.currentLocation?.currentEvent != null)
                return;

            Farmer farmer = Game1.player;
            Rectangle farmerBox = Game1.player.GetBoundingBox();
            
            farmerBox.Inflate(64, 64);

            foreach (NPC npc in farmer.currentLocation.characters) {
                Rectangle npcBox = npc.GetBoundingBox();
                bool isNpcAtCursorTile = Helper.IsNPCAtTile(farmer.currentLocation, e.Cursor.Tile, npc)
                                         || Helper.IsNPCAtTile(farmer.currentLocation, e.Cursor.Tile + new Vector2(0f, 1f), npc)
                                         || Helper.IsNPCAtTile(farmer.currentLocation, e.Cursor.GrabTile, npc);

                if (actionButtonPressed && farmerBox.Intersects(npcBox) && isNpcAtCursorTile)
                {
                    if (Helper.CanRequestDialog(farmer, npc) && !CheckForNewNonMarriageDialogue(farmer, npc))
                    {
                        this.RequestDialogue(farmer, npc, 0);
                    }
                    break;
                }
            }

        }

        private static bool CheckForNewNonMarriageDialogue(Farmer farmer, NPC npc)
        {
            if (Helper.IsSpouseMarriedToFarmer(npc, farmer) || !farmer.friendshipData.ContainsKey(npc.Name))
                return false;

            if (npc.checkForNewCurrentDialogue(farmer.friendshipData[npc.Name].Points / 250, false))
                return true;
            
            return npc.checkForNewCurrentDialogue(farmer.friendshipData[npc.Name].Points / 250, true);
        }

        private void WatchDialogue()
        {
            // Check if speaker is changed
            if (Game1.currentSpeaker != this.CurrentSpeaker)
            {
                if (Game1.currentSpeaker != null)
                    this.OnSpeakerChange(this.CurrentSpeaker, Game1.currentSpeaker);
                this.CurrentSpeaker = Game1.currentSpeaker;
            }

            if (this.CurrentSpeaker == null && this.CurrentDialogue == null)
                return; // Nobody speaking, no dialogue can be changed

            Dialogue dialogue = null;

            if (this.CurrentSpeaker?.CurrentDialogue?.Count > 0)
                dialogue = this.CurrentSpeaker.CurrentDialogue.Peek();

            // Check if dialogue is changed
            if (this.CurrentDialogue != dialogue)
            {
                this.OnChangeDialogue(this.CurrentDialogue, dialogue, this.CurrentSpeaker?.CurrentDialogue?.Count <= 1);
                this.TryRemember(dialogue);
                this.CurrentDialogue = dialogue;
            }
        }

        private void TryRemember(Dialogue dialogue)
        {
            if (dialogue is CompanionDialogue companionDialogue && companionDialogue.Remember && !string.IsNullOrEmpty(companionDialogue.Tag))
            {
                if (!Game1.player.hasOrWillReceiveMail(companionDialogue.Tag))
                    Game1.player.mailReceived.Add(companionDialogue.Tag);
            }
        }

        private void OnChangeDialogue(Dialogue previousDialogue, Dialogue currentDialogue, bool isLastDialogue = false)
        {
            if (this.DialogueChanged == null)
                return;

            DialogueChangedArgs args = new DialogueChangedArgs
            {
                PreviousDialogue = previousDialogue,
                CurrentDialogue = currentDialogue,
                IsLastDialogue = isLastDialogue
            };

            this.DialogueChanged(this, args);
        }

        private void OnSpeakerChange(NPC previousSpeaker, NPC currentSpeaker)
        {
            if (this.SpeakerChanged == null)
                return;

            SpeakerChangedArgs args = new SpeakerChangedArgs()
            {
                CurrentSpeaker = currentSpeaker,
                PreviousSpeaker = previousSpeaker
            };

            this.SpeakerChanged(this, args);
        }
    }

    public class DialogueChangedArgs : EventArgs
    {
        public Dialogue CurrentDialogue { get; set; }
        public Dialogue PreviousDialogue { get; set; }
        public bool IsLastDialogue { get; set; }
    }

    public class SpeakerChangedArgs : EventArgs
    {
        public NPC CurrentSpeaker { get; set; }
        public NPC PreviousSpeaker { get; set; }
    }

    public class DialogueRequestArgs : EventArgs
    {
        public Farmer Initiator { get; set; }
        public NPC WithWhom { get; set; }
        public int RequestId { get; set; }
    }
}
