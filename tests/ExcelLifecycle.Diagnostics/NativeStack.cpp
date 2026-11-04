#include <windows.h>
#include <cstdio>
#include <cstdlib>
#include "dbgeng.h"

class ConsoleOutput : public IDebugOutputCallbacks {
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** p) override {
        if (iid == __uuidof(IUnknown) || iid == __uuidof(IDebugOutputCallbacks)) { *p = this; return S_OK; }
        *p = nullptr; return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }
    HRESULT STDMETHODCALLTYPE Output(ULONG, PCSTR text) override { fputs(text, stdout); return S_OK; }
};

int main(int argc, char** argv) {
    if (argc != 2) return 2;
    IDebugClient* client = nullptr;
    HRESULT hr = DebugCreate(__uuidof(IDebugClient), (void**)&client);
    if (FAILED(hr)) { printf("DebugCreate %08x\n", hr); return 1; }
    ConsoleOutput output;
    client->SetOutputCallbacks(&output);
    IDebugControl* control = nullptr;
    client->QueryInterface(__uuidof(IDebugControl), (void**)&control);
    hr = client->AttachProcess(0, strtoul(argv[1], nullptr, 10), DEBUG_ATTACH_NONINVASIVE);
    if (SUCCEEDED(hr)) {
        hr = control->WaitForEvent(0, 5000);
        if (SUCCEEDED(hr)) control->Execute(DEBUG_OUTCTL_THIS_CLIENT,
            "~0s; r; u mso20win32client!Ordinal53733 L40; ub mso20win32client!Ordinal63529+0x628 L20; ~* k 12",
            DEBUG_EXECUTE_DEFAULT);
        else printf("WaitForEvent %08x\n", hr);
        client->DetachProcesses();
    } else printf("AttachProcess %08x\n", hr);
    control->Release();
    client->SetOutputCallbacks(nullptr);
    client->Release();
    return FAILED(hr) ? 1 : 0;
}
