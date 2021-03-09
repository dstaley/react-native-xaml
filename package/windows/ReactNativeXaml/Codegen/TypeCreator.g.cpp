#include "pch.h"
#include "XamlMetadata.h"
#include "Crc32Str.h"
#include <winstring.h>

/*************************************************************
THIS FILE WAS AUTOMATICALLY GENERATED, DO NOT MODIFY MANUALLY
**************************************************************/

winrt::Windows::Foundation::IInspectable XamlMetadata::Create(const std::string_view& typeName) const {
  wchar_t buf[128]{};
  for (auto i = 0u; i < typeName.size() && i < ARRAYSIZE(buf) - 1; i++) {
    buf[i] = static_cast<wchar_t>(typeName[i]);
  }

  HSTRING clsid = nullptr;
  if (SUCCEEDED(WindowsCreateString(buf, static_cast<UINT32>(wcslen(buf)), &clsid))) {
    winrt::com_ptr<::IInspectable> insp{ nullptr };
    if (SUCCEEDED(RoActivateInstance(clsid, insp.put()))) {
      winrt::IUnknown unk{ nullptr };
      winrt::copy_from_abi(unk, insp.get());
      WindowsDeleteString(clsid);
      return unk.as<winrt::IInspectable>();
    }
  }
  WindowsDeleteString(clsid);
  assert(false && "xaml type not found");
  return nullptr;
}


