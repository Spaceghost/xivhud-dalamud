using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using XivHud.Windowing.Dalamud;

namespace XivHud.NativeWindowing.Tests;

public sealed unsafe class NativeWindowCaptureTests
{
    private static readonly Vector2 Position = new(100, 200), Size = new(900, 700);

    [Fact]
    public void CapturesOneRootItsChildrenAndOwnPopupTogetherWithoutTouchingOtherWindows()
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position, Size);
        var child = fixture.Window(Position + new Vector2(20), new(200));
        var popup = fixture.Window(Position + new Vector2(300, 100), new(400));
        var unrelated = fixture.Window(Position, Size);
        var otherPopup = fixture.Window(Position, Size);
        fixture.Child(root, child);
        child.RootWindowPopupTree = root;
        popup.RootWindowPopupTree = root;
        popup.Flags = ImGuiWindowFlags.Popup;
        otherPopup.RootWindowPopupTree = unrelated;
        otherPopup.Flags = ImGuiWindowFlags.Popup;
        var seen = new List<nint>();
        var result = NativeWindowCapture.DrawOwned(root, [root, child, unrelated, popup, otherPopup], Position, Size, frame =>
        {
            Assert.Equal(3, frame.Data->CmdListsCount);
            Assert.Equal(9, frame.Data->TotalIdxCount);
            Assert.Equal(9, frame.Data->TotalVtxCount);
            Assert.Equal(Position, frame.Data->DisplayPos);
            for (var i = 0; i < frame.Data->CmdListsCount; i++) seen.Add((nint)frame.Data->CmdLists[i]);
            return true;
        });
        Assert.True(result);
        Assert.Equal(new[] { (nint)root.DrawList.Handle, (nint)child.DrawList.Handle, (nint)popup.DrawList.Handle }, seen);
        Assert.Equal(0, root.DrawList.CmdBuffer.Size);
        Assert.Equal(0, child.DrawList.CmdBuffer.Size);
        Assert.Equal(0, popup.DrawList.CmdBuffer.Size);
        Assert.Equal(1, unrelated.DrawList.CmdBuffer.Size);
        Assert.Equal(1, otherPopup.DrawList.CmdBuffer.Size);
        Assert.Equal(3, root.DrawList.VtxBuffer.Size);
    }

    [Fact]
    public void FloatingCaptureCanIncludeOwnedPopupOutsideRootWithAnExpandedTexture()
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position, Size);
        var popup = fixture.Window(Position + new Vector2(850, 100), new(300, 200));
        popup.RootWindowPopupTree = root; popup.Flags = ImGuiWindowFlags.Popup;
        var expanded = new Vector2(1150, 700);
        Assert.True(NativeWindowCapture.DrawOwned(root, [root, popup], Position, expanded, frame =>
        {
            Assert.Equal(expanded, frame.Data->DisplaySize);
            Assert.Equal(2, frame.Data->CmdListsCount); return true;
        }, true));
        Assert.Equal(0, popup.DrawList.CmdBuffer.Size);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsuccessfulTextureSubmissionLeavesOriginalContentIntact(bool throws)
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position, Size);
        if (throws)
            Assert.Throws<InvalidOperationException>(() => NativeWindowCapture.DrawOwned(root, [root], Position, Size,
                _ => throw new InvalidOperationException("texture failed")));
        else Assert.False(NativeWindowCapture.DrawOwned(root, [root], Position, Size, _ => false));
        Assert.Equal(1, root.DrawList.CmdBuffer.Size);
    }

    [Fact]
    public void OutOfBoundsPopupFallsBackBeforeSubmittingOrSuppressingAnyOwnedList()
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position, Size);
        var popup = fixture.Window(Position + new Vector2(850, 0), new(200));
        popup.RootWindowPopupTree = root;
        popup.Flags = ImGuiWindowFlags.Popup;
        var submitted = false;
        Assert.Throws<InvalidOperationException>(() => NativeWindowCapture.DrawOwned(root, [root, popup], Position, Size,
            _ => { submitted = true; return true; }));
        Assert.False(submitted);
        Assert.Equal(1, root.DrawList.CmdBuffer.Size);
        Assert.Equal(1, popup.DrawList.CmdBuffer.Size);
    }

    [Fact]
    public void HiddenOrInactiveChildDoesNotLeakOldDrawCommandsIntoNativeFrame()
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position, Size);
        var child = fixture.Window(Position, Size);
        fixture.Child(root, child);
        child.Hidden = true;
        Assert.True(NativeWindowCapture.DrawOwned(root, [root, child], Position, Size, frame =>
        { Assert.Equal(1, frame.Data->CmdListsCount); return true; }));
        Assert.Equal(1, child.DrawList.CmdBuffer.Size);
    }

    [Fact]
    public void UnsafeCallbackIsRefusedBeforeTextureSubmission()
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position, Size);
        root.DrawList.CmdBuffer.Data->UserCallback = (delegate* unmanaged<ImDrawList*, ImDrawCmd*, void>)1;
        var submitted = false;
        Assert.Throws<InvalidOperationException>(() => NativeWindowCapture.DrawOwned(root, [root], Position, Size,
            _ => { submitted = true; return true; }));
        Assert.False(submitted);
        Assert.Equal(1, root.DrawList.CmdBuffer.Size);
    }

    [Fact]
    public void MisalignedInputRectangleCannotBeHiddenAsIfNativeContentMatched()
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position + new Vector2(30), Size);
        Assert.Throws<InvalidOperationException>(() => NativeWindowCapture.DrawOwned(root, [root], Position, Size, _ => true));
        Assert.Equal(1, root.DrawList.CmdBuffer.Size);
    }

    [Fact]
    public void ForeignImGuiFocusDefeatsStaleNativeFocusEvenBeforeForeignWindowDraws()
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position, Size);
        var foreign = fixture.Window(Position, Size);
        foreign.Active = false;
        foreign.WasActive = true;
        Assert.False(NativeWindowFocus.HasFocus(true, root, foreign));
        foreign.WasActive = false;
        Assert.False(NativeWindowFocus.HasFocus(true, root, foreign));
    }

    [Fact]
    public void OwnChildAndPopupMayUseNativeFocusButNeverWithoutNativeAuthority()
    {
        using var fixture = new Fixture();
        var root = fixture.Window(Position, Size);
        var child = fixture.Window(Position, Size);
        var popup = fixture.Window(Position, Size);
        child.RootWindow = root;
        popup.RootWindowPopupTree = root;
        Assert.True(NativeWindowFocus.HasFocus(true, root, root));
        Assert.True(NativeWindowFocus.HasFocus(true, root, child));
        Assert.True(NativeWindowFocus.HasFocus(true, root, popup));
        Assert.True(NativeWindowFocus.HasFocus(true, root, default));
        Assert.False(NativeWindowFocus.HasFocus(false, root, root));
        Assert.False(NativeWindowFocus.HasFocus(false, root, popup));
        Assert.False(NativeWindowFocus.HasFocus(false, root, default));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<nint> blocks = [];
        private T* Allocate<T>(int count = 1) where T : unmanaged
        {
            var value = (T*)NativeMemory.AllocZeroed((nuint)(sizeof(T) * count));
            blocks.Add((nint)value);
            return value;
        }
        public ImGuiWindowPtr Window(Vector2 position, Vector2 size)
        {
            var window = new ImGuiWindowPtr(Allocate<ImGuiWindow>());
            window.Active = true; window.Pos = position; window.Size = size;
            var list = Allocate<ImDrawList>();
            var command = Allocate<ImDrawCmd>(); command->ElemCount = 3;
            list->CmdBuffer = new(1, 1, command);
            list->IdxBuffer = new(3, 3, Allocate<ushort>(3));
            list->VtxBuffer = new(3, 3, Allocate<ImDrawVert>(3));
            window.DrawList = new ImDrawListPtr(list);
            return window;
        }
        public void Child(ImGuiWindowPtr parent, ImGuiWindowPtr child)
        {
            var children = Allocate<ImGuiWindowPtr>(); *children = child;
            parent.DC.ChildWindows = new(1, 1, children);
        }
        public void Dispose() { foreach (var block in blocks) NativeMemory.Free((void*)block); }
    }
}
