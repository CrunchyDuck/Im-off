using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;


// Much of this code is lifted straight from LendColonistsToFaction. I stripped out what I needed to get my idea to work.
namespace CrunchyDuck {
	public class QuestNode_GiveColonistsToFaction : QuestNode {
		[NoTranslate]
		public SlateRef<string> inSignalEnable;
		[NoTranslate]
		public SlateRef<string> outSignalComplete;
		[NoTranslate]
		public SlateRef<string> outSignalColonistsDied;
		public SlateRef<Thing> shuttle;
		public SlateRef<Pawn> lendColonistsToFactionOf;
		public SlateRef<int> returnLentColonistsInTicks;

		protected override void RunInt() {
			Slate slate = QuestGen.slate;
			string inSignal = QuestGenUtility.HardcodedSignalWithQuestID(inSignalEnable.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignal");
			QuestPart_GiveColonistsToFaction colonistsToFaction = new QuestPart_GiveColonistsToFaction();
			colonistsToFaction.inSignalEnable = inSignal;
			colonistsToFaction.shuttle = shuttle.GetValue(slate);
			colonistsToFaction.lendColonistsToFaction = lendColonistsToFactionOf.GetValue(slate).Faction;
			colonistsToFaction.colonistsArriveInTicks = 2500 * UnityEngine.Random.Range(1, 3); //returnLentColonistsInTicks.GetValue(slate);
			//colonistsToFaction.returnMap = slate.Get<Map>("map").Parent;
			QuestPart_GiveColonistsToFaction part = colonistsToFaction;
			if (!outSignalComplete.GetValue(slate).NullOrEmpty())
				part.outSignalsCompleted.Add(QuestGenUtility.HardcodedSignalWithQuestID(outSignalComplete.GetValue(slate)));
			if (!outSignalColonistsDied.GetValue(slate).NullOrEmpty())
				part.outSignalColonistsDied = QuestGenUtility.HardcodedSignalWithQuestID(outSignalColonistsDied.GetValue(slate));
			QuestGen.quest.AddPart(part);
			QuestGen.quest.TendPawnsWithMedicine(ThingDefOf.MedicineIndustrial, pawnsInTransporter: shuttle.GetValue(slate), inSignal: inSignal);
		}

		protected override bool TestRunInt(Slate slate) => true;
	}

	public class QuestPart_GiveColonistsToFaction : QuestPartActivable {
		public Thing shuttle;
		public Faction lendColonistsToFaction;
		public int colonistsArriveInTicks = -1;
		//public MapParent returnMap;
		public string outSignalColonistsDied;
		private int returnColonistsOnTick;
		private List<Thing> lentColonists = new List<Thing>();

		public List<Thing> LentColonistsListForReading => lentColonists;

		public int ReturnPawnsInDurationTicks => Mathf.Max(returnColonistsOnTick - GenTicks.TicksGame, 0);

		protected override void Enable(SignalArgs receivedArgs) {
			base.Enable(receivedArgs);
			CompTransporter comp = shuttle.TryGetComp<CompTransporter>();
			if (lendColonistsToFaction == null || comp == null)
				return;
			foreach (Thing thing in comp.innerContainer) {
				if (thing is Pawn pawn && pawn.IsFreeColonist)
					lentColonists.Add(pawn);
			}
			foreach (Pawn p in lentColonists) {
				p.SetFaction(lendColonistsToFaction);
			}
			returnColonistsOnTick = GenTicks.TicksGame + colonistsArriveInTicks;
			//base.Complete(new SignalArgs(new LookTargets(lentColonists).Named("SUBJECT"), lentColonists.Select(c => c.LabelShort).ToCommaList(true).Named("PAWNS")));
		}

		public override string DescriptionPart => State == QuestPartState.Disabled || lentColonists.Count == 0 ? (string)null : (string)"PawnsLent".Translate((NamedArgument)lentColonists.Select((Func<Thing, string>)(t => t.LabelShort)).ToCommaList(true), (NamedArgument)ReturnPawnsInDurationTicks.ToStringTicksToDays("0.0"));

		// Delay for the colonists to arrive at the base.
		// Actually, it's because I couldn't figure out how to make the quest reward drop otherwise.
		// If the quest ends before the shuttle leaves the map, the reward for the quest isn't dropped. So, I made a lore reason.
		public override void QuestPartTick() {
			base.QuestPartTick();
			if (Find.TickManager.TicksGame < enableTick + colonistsArriveInTicks)
				return;
			Complete(new SignalArgs(new LookTargets(lentColonists).Named("SUBJECT"), lentColonists.Select(c => c.LabelShort).ToCommaList(true).Named("PAWNS")));
		}

		//protected override void Complete(SignalArgs signalArgs) {
		//	Map map = returnMap == null ? Find.AnyPlayerHomeMap : returnMap.Map;
		//	if (map == null)
		//		return;
		//	base.Complete(new SignalArgs(new LookTargets((IEnumerable<Thing>)lentColonists).Named("SUBJECT"), lentColonists.Select((Func<Thing, string>)(c => c.LabelShort)).ToCommaList(true).Named("PAWNS")));
		//	if (lendColonistsToFaction != null && lendColonistsToFaction == Faction.OfEmpire) {
		//		Thing shipThing = ThingMaker.MakeThing(ThingDefOf.Shuttle);
		//		shipThing.SetFaction(Faction.OfEmpire);
		//		TransportShip transportShip = TransportShipMaker.MakeTransportShip(TransportShipDefOf.Ship_Shuttle, (IEnumerable<Thing>)lentColonists, shipThing);
		//		transportShip.ArriveAt(DropCellFinder.GetBestShuttleLandingSpot(map, Faction.OfEmpire), map.Parent);
		//		transportShip.AddJobs(ShipJobDefOf.Unload, ShipJobDefOf.FlyAway);
		//	}
		//	else
		//		DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, (IEnumerable<Thing>)lentColonists, canRoofPunch: false, forbid: false);
		//}

		// I'm not quite sure when this is invoked, so I don't want to remove it.
		public override void Notify_PawnKilled(Pawn pawn, DamageInfo? dinfo) {
			if (!lentColonists.Contains((Thing)pawn))
				return;
			Building_Grave assignedGrave = (Building_Grave)null;
			if (pawn.ownership != null)
				assignedGrave = pawn.ownership.AssignedGrave;
			Corpse val = pawn.MakeCorpse(assignedGrave, false, 0.0f);
			lentColonists.Remove((Thing)pawn);
			Map anyPlayerHomeMap = Find.AnyPlayerHomeMap;
			if (anyPlayerHomeMap != null)
				DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(anyPlayerHomeMap), anyPlayerHomeMap, (IEnumerable<Thing>)Gen.YieldSingle(val), canRoofPunch: false, forbid: false);
			if (outSignalColonistsDied.NullOrEmpty() || lentColonists.Count != 0)
				return;
			Find.SignalManager.SendSignal(new Signal(outSignalColonistsDied));
		}

		public override void DoDebugWindowContents(Rect innerRect, ref float curY) {
			if (State != QuestPartState.Enabled)
				return;
			Rect rect = new Rect(innerRect.x, curY, 500f, 25f);
			if (Widgets.ButtonText(rect, "End " + ToString()))
				Complete();
			curY += rect.height + 4f;
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_References.Look(ref shuttle, "shuttle");
			Scribe_References.Look(ref lendColonistsToFaction, "lendColonistsToFaction");
			Scribe_Values.Look(ref colonistsArriveInTicks, "colonistsArriveInTicks");
			Scribe_Values.Look(ref returnColonistsOnTick, "colonistsReturnOnTick");
			Scribe_Collections.Look(ref lentColonists, "lentPawns", LookMode.Reference);
			//Scribe_References.Look(ref returnMap, "returnMap");
			Scribe_Values.Look(ref outSignalColonistsDied, "outSignalColonistsDied");
			if (Scribe.mode != LoadSaveMode.PostLoadInit)
				return;
			lentColonists.RemoveAll(x => x == null);
		}
	}
}