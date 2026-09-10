using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DialogueEditing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using RPGProject.Feature.Dialogue;

public sealed class DialogueEditorTests
{
    private DialogueDocument document;
    private string directory;
    [SetUp] public void SetUp()
    {
        document = ScriptableObject.CreateInstance<DialogueDocument>();
        directory = Path.Combine(Path.GetTempPath(), "DialogueEditorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }
    [TearDown] public void TearDown()
    {
        Undo.ClearUndo(document); Object.DestroyImmediate(document);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test] public void CsvRoundTripPreservesCommasQuotesNewlinesKoreanAndEmptyCells()
    {
        var row = document.AddTalk("E");
        row.Text = "안녕, \"여행자\"\r\n다음 줄\n마지막 줄";
        row.Name = "\"이름\""; row.NextID = "";
        string encoded = document.Encode();
        var decoded = DialogueDocument.Decode("\uFEFF" + encoded);
        CollectionAssert.AreEqual(row.Cells(), decoded.Single().Cells());
        var runtime = DialogueCsv.Read(encoded);
        Assert.That(runtime[0]["Text"], Is.EqualTo(row.Text));
        Assert.That(runtime[0]["Name"], Is.EqualTo(row.Name));
    }
    [TestCase("a,b\n\"unclosed,b")]
    [TestCase("a,b\nx\"y,z")]
    [TestCase("a,b\n\"x\"oops,z")]
    [TestCase("a,a\nx,y")]
    [TestCase("a,b\nx,y,z")]
    public void BrokenCsvIsRejectedInsteadOfSilentlyDroppingData(string input)
    { Assert.Throws<FormatException>(() => DialogueCsv.Read(input)); }

    [Test] public void CsvSupportsTrailingEmptyFieldAndNoFinalNewline()
    {
        var rows = DialogueCsv.Read("a,b,c\r\n1,2,");
        Assert.That(rows.Single()["c"], Is.EqualTo(""));
    }
    [Test] public void SeparatorRowsDoNotBecomeEvents()
    {
        string csv = document.Encode() + ",,,,,,,,,\n\n";
        Assert.That(DialogueDocument.Decode(csv), Is.Empty);
    }
    [Test] public void WrongHeaderIsRejected()
    { Assert.Throws<FormatException>(() => DialogueDocument.Decode("EventID,Seq\nE,1")); }
    [Test] public void CurrentEventCsvRoundTripsEveryNonemptyRecord()
    {
        string source = File.ReadAllText(DialogueFileStore.CsvPath);
        var original = DialogueDocument.Decode(source);
        document.rows = original;
        var after = DialogueDocument.Decode(document.Encode());
        Assert.That(after.Count, Is.EqualTo(original.Count));
        for (int i = 0; i < original.Count; i++) CollectionAssert.AreEqual(original[i].Cells(), after[i].Cells());
    }
    [Test] public void ChoiceTemplateIsValidAndBranchesRemainAdjacentWhenMoved()
    {
        var talk = document.AddTalk("E"); var choice = document.AddChoice("E");
        talk.NextID = choice.Seq;
        Assert.That(DialogueValidation.Check(document).Any(i => i.error), Is.False);
        document.MoveBlock(choice, -1);
        CollectionAssert.AreEqual(new[] { "CHOICE", "BRANCH", "BRANCH", "TALK" }, document.Event("E").Select(r => r.Type));
    }
    [Test] public void DuplicateChoiceRemapsItsInternalReferencesAndCreatesDistinctKeys()
    {
        var choice = document.AddChoice("E");
        var original = document.Block(choice);
        original[1].NextID = choice.Seq;
        var copy = document.Duplicate(choice);
        var copied = document.Block(copy);
        Assert.That(copied.Count, Is.EqualTo(3));
        Assert.That(copied[1].NextID, Is.EqualTo(copy.Seq));
        Assert.That(copied[2].NextID, Is.EqualTo("END"));
        Assert.That(copy.NextID, Is.EqualTo(string.Join(",", copied.Skip(1).Select(r => r.Seq))));
        Assert.That(document.rows.Select(r => r.key).Distinct().Count(), Is.EqualTo(document.rows.Count));
    }
    [Test] public void DeletingTargetLeavesVisibleErrorAndNewIdsDoNotRetargetDanglingLinks()
    {
        var a = document.AddTalk("E"); var b = document.AddTalk("E");
        a.NextID = b.Seq; document.Delete(b);
        var c = document.AddTalk("E");
        Assert.That(c.Seq, Is.Not.EqualTo(b.Seq));
        Assert.That(DialogueValidation.Check(document).Any(i => i.error && i.row == a && i.message.Contains(b.Seq)), Is.True);
    }
    [Test] public void SeqValuesMayRepeatAcrossEventsButNotInsideAnEvent()
    {
        var a = document.AddTalk("A"); var b = document.AddTalk("B");
        Assert.That(a.Seq, Is.EqualTo(b.Seq));
        Assert.That(DialogueValidation.Check(document).Any(i => i.error), Is.False);
        var duplicate = document.AddTalk("A"); duplicate.Seq = a.Seq;
        Assert.That(DialogueValidation.Check(document).Any(i => i.error && i.message.Contains("중복")), Is.True);
    }
    [TestCase("HASITEM:Potion:3", "")]
    [TestCase("HASITEM:Gold_-1", "")]
    [TestCase("", "SET_FLAG:passed_guard")]
    [TestCase("", "SET_FLAG:passed_guard:yes")]
    [TestCase("", "REMOVE:Potion:-1")]
    [TestCase("", "BATTLE:guard_mob")]
    public void UnsupportedOrMalformedCommandsAreErrors(string condition, string action)
    {
        var row = document.AddBranch(document.AddChoice("E")); row.Condition = condition; row.Action = action;
        Assert.That(DialogueValidation.Check(document).Any(i => i.error && i.row == row), Is.True);
    }
    [Test] public void SupportedConditionsAndActionsValidate()
    {
        var row = document.AddBranch(document.AddChoice("E"));
        row.Condition = "FLAG:open;HASITEM:Gold_100;HASITEM:Potion";
        row.Action = "REMOVE:Gold_100;REMOVE:Potion:2;SET_FLAG:open:false";
        Assert.That(DialogueValidation.Check(document).Any(i => i.error), Is.False);
    }
    [Test] public void ActionsOnTalkAreReportedAsNotExecutedByRuntime()
    {
        var row = document.AddTalk("E"); row.Action = "SET_FLAG:open:true";
        Assert.That(DialogueValidation.Check(document).Any(i => !i.error && i.row == row && i.message.Contains("효과")), Is.True);
    }
    [Test] public void InvisibleCycleIsRejectedAndInteractiveCycleIsAllowed()
    {
        var row = document.AddTalk("E"); row.NextID = row.Seq;
        Assert.That(DialogueValidation.Check(document).Any(i => i.error), Is.False);
        row.Type = "JOIN"; row.Text = ""; row.CharacterID = "chr_00";
        Assert.That(DialogueValidation.Check(document).Any(i => i.error && i.message.Contains("반복")), Is.True);
    }
    [Test] public void EveryConditionalChoiceProducesWarning()
    {
        var choice = document.AddChoice("E");
        foreach (var branch in document.Block(choice).Skip(1)) branch.Condition = "FLAG:open";
        Assert.That(DialogueValidation.Check(document).Any(i => !i.error && i.row == choice), Is.True);
    }
    [Test] public void ReorderingChoiceCanBeUndoneWithItsBranches()
    {
        document.AddTalk("E"); var choice = document.AddChoice("E"); string before = document.Encode();
        Undo.RegisterCompleteObjectUndo(document, "Move"); document.MoveBlock(choice, -1);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Assert.That(document.Encode(), Is.EqualTo(before));
    }
    [Test] public void ExternalEditIsNotOverwritten()
    {
        string path = Path.Combine(directory, "EventScripts.csv"); File.WriteAllText(path, "original");
        string hash = DialogueFileStore.Hash(path); File.WriteAllText(path, "external change");
        Assert.Throws<IOException>(() => DialogueFileStore.Save(path, "new", hash, Path.Combine(directory, "backups")));
        Assert.That(File.ReadAllText(path), Is.EqualTo("external change"));
    }
    [Test] public void SuccessfulSaveKeepsPreviousFileAsBackup()
    {
        string path = Path.Combine(directory, "EventScripts.csv"); File.WriteAllText(path, "original");
        string backup = DialogueFileStore.Save(path, "new", DialogueFileStore.Hash(path), Path.Combine(directory, "backups"));
        Assert.That(File.ReadAllText(path), Is.EqualTo("new"));
        Assert.That(File.ReadAllText(backup), Is.EqualTo("original"));
        Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
    }
}
