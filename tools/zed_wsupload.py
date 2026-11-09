"""ZED ZONE Steam Workshop uploader and localized description/dependency manager (AppID 1211600).

Uses G:\\Steam\\steamapps\\common\\ZED ZONE\\ZEDZONE_Data\\Plugins\\x86_64\\steam_api64.dll (ISteamUGC v016).
"""
import ctypes as C
import glob
import os
import sys
import time

APP_ID = 1211600
CREATE_ITEM_RESULT = 3403         # CreateItemResult_t::k_iCallback
SUBMIT_ITEM_UPDATE_RESULT = 3404  # SubmitItemUpdateResult_t::k_iCallback
ADD_UGC_DEPENDENCY_RESULT = 3412  # AddUGCDependencyResult_t::k_iCallback
VISIBILITY = {"public": 0, "friendsOnly": 1, "private": 2, "unlisted": 3}


class CreateItemResult(C.Structure):
    _fields_ = [("result", C.c_int32), ("item", C.c_uint64), ("legal", C.c_bool)]


class SubmitItemUpdateResult(C.Structure):
    _fields_ = [("result", C.c_int32), ("legal", C.c_bool), ("item", C.c_uint64)]


class AddUGCDependencyResult(C.Structure):
    _fields_ = [("result", C.c_int32), ("item", C.c_uint64), ("child", C.c_uint64)]


def fail(msg):
    print("FAILED:", msg, flush=True)
    sys.exit(1)


def read_page(path):
    page, lines = {"id": None, "title": "", "visibility": "public"}, []
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.strip()
            if not line or line.startswith("#") or line.startswith("//"):
                continue
            key, _, val = line.partition("=")
            if key == "description":
                if lines or val:
                    lines.append(val)
            elif key in ("id", "title", "visibility"):
                page[key] = val
    page["description"] = "\n".join(lines)
    return page


def read_requires(folder):
    path = os.path.join(folder, "requires.txt")
    if not os.path.isfile(path):
        return []
    ids = []
    for line in open(path, encoding="utf-8-sig"):
        line = line.split("#", 1)[0].strip()
        if line:
            ids.append(int(line))
    return ids


def init_steam():
    game_dir = r"G:\Steam\steamapps\common\ZED ZONE"
    dll_dir = os.path.join(game_dir, "ZEDZONE_Data", "Plugins", "x86_64")
    os.environ["SteamAppId"] = str(APP_ID)
    os.environ["SteamGameId"] = str(APP_ID)
    os.chdir(game_dir)
    api = C.CDLL(os.path.join(dll_dir, "steam_api64.dll"))
    u64, ptr = C.c_uint64, C.c_void_p
    for name, res, args in [
        ("SteamAPI_Init", C.c_bool, []),
        ("SteamAPI_Shutdown", None, []),
        ("SteamAPI_SteamUGC_v016", ptr, []),
        ("SteamAPI_SteamUtils_v010", ptr, []),
        ("SteamAPI_ISteamUtils_GetAppID", C.c_uint32, [ptr]),
        ("SteamAPI_ISteamUGC_CreateItem", u64, [ptr, C.c_uint32, C.c_int]),
        ("SteamAPI_ISteamUGC_StartItemUpdate", u64, [ptr, C.c_uint32, u64]),
        ("SteamAPI_ISteamUGC_SetItemUpdateLanguage", C.c_bool, [ptr, u64, C.c_char_p]),
        ("SteamAPI_ISteamUGC_SetItemTitle", C.c_bool, [ptr, u64, C.c_char_p]),
        ("SteamAPI_ISteamUGC_SetItemDescription", C.c_bool, [ptr, u64, C.c_char_p]),
        ("SteamAPI_ISteamUGC_SetItemVisibility", C.c_bool, [ptr, u64, C.c_int]),
        ("SteamAPI_ISteamUGC_SetItemContent", C.c_bool, [ptr, u64, C.c_char_p]),
        ("SteamAPI_ISteamUGC_SetItemPreview", C.c_bool, [ptr, u64, C.c_char_p]),
        ("SteamAPI_ISteamUGC_SubmitItemUpdate", u64, [ptr, u64, C.c_char_p]),
        ("SteamAPI_ISteamUGC_AddDependency", u64, [ptr, u64, u64]),
        ("SteamAPI_ISteamUtils_IsAPICallCompleted", C.c_bool, [ptr, u64, C.POINTER(C.c_bool)]),
        ("SteamAPI_ISteamUtils_GetAPICallResult", C.c_bool, [ptr, u64, ptr, C.c_int, C.c_int, C.POINTER(C.c_bool)]),
        ("SteamAPI_ISteamUtils_GetAPICallFailureReason", C.c_int, [ptr, u64]),
    ]:
        fn = getattr(api, name)
        fn.restype, fn.argtypes = res, args

    if not api.SteamAPI_Init():
        fail("SteamAPI_Init failed (is Steam client running?)")
    ugc = api.SteamAPI_SteamUGC_v016()
    utils = api.SteamAPI_SteamUtils_v010()
    if not ugc or not utils or api.SteamAPI_ISteamUtils_GetAppID(utils) != APP_ID:
        fail("SteamAPI AppID mismatch")
    return api, ugc, utils


def wait_call(api, utils, call, struct_cls, callback_id, label):
    failed = C.c_bool()
    deadline = time.time() + 180
    while time.time() < deadline:
        if api.SteamAPI_ISteamUtils_IsAPICallCompleted(utils, call, C.byref(failed)):
            if failed.value:
                reason = api.SteamAPI_ISteamUtils_GetAPICallFailureReason(utils, call)
                fail(f"{label}: Steam API call failed (reason={reason})")
            res = struct_cls()
            if not api.SteamAPI_ISteamUtils_GetAPICallResult(utils, call, C.byref(res), C.sizeof(res), callback_id, C.byref(failed)):
                fail(f"{label}: GetAPICallResult failed")
            return res
        time.sleep(0.2)
    fail(f"{label}: timed out waiting for Steam")


def main():
    if len(sys.argv) < 2:
        fail("usage: zed_wsupload.py <staging_folder> [change_note]")
    stage = os.path.abspath(sys.argv[1])
    change_note = sys.argv[2] if len(sys.argv) > 2 else "v1.0.0 release"
    ws_txt = os.path.join(stage, "workshop.txt")
    main_page = read_page(ws_txt)

    api, ugc, utils = init_steam()
    try:
        item_id = int(main_page["id"]) if main_page["id"] else 0
        if item_id == 0:
            print("Creating new Steam Workshop item for ZED ZONE (1211600)...", flush=True)
            call = api.SteamAPI_ISteamUGC_CreateItem(ugc, APP_ID, 0)
            res = wait_call(api, utils, call, CreateItemResult, CREATE_ITEM_RESULT, "CreateItem")
            if res.result != 1:
                fail(f"CreateItem EResult={res.result}")
            item_id = int(res.item)
            print(f"Created Workshop item ID: {item_id} (legal={res.legal})", flush=True)
            # Prepend id= into workshop.txt
            txt = open(ws_txt, encoding="utf-8").read()
            txt = txt.replace("NEW_WORKSHOP_ID", str(item_id))
            if "\nid=" not in ("\n" + txt):
                txt = f"id={item_id}\n" + txt
            open(ws_txt, "w", encoding="utf-8").write(txt)
            for p in glob.glob(os.path.join(stage, "workshop_*.txt")):
                ptxt = open(p, encoding="utf-8").read().replace("NEW_WORKSHOP_ID", str(item_id))
                open(p, "w", encoding="utf-8").write(ptxt)
            main_page = read_page(ws_txt)

        # 1. Upload Contents + Preview + Visibility + English title/desc
        contents_dir = os.path.join(stage, "Contents")
        preview_png = os.path.join(stage, "preview.png")
        if not os.path.isdir(contents_dir):
            fail("Missing Contents directory: " + contents_dir)
        if not os.path.isfile(preview_png):
            fail("Missing preview.png: " + preview_png)
        if os.path.getsize(preview_png) >= 1024 * 1024:
            fail(f"preview.png is >= 1 MiB ({os.path.getsize(preview_png)} bytes)")

        h = api.SteamAPI_ISteamUGC_StartItemUpdate(ugc, APP_ID, item_id)
        api.SteamAPI_ISteamUGC_SetItemUpdateLanguage(ugc, h, b"english")
        api.SteamAPI_ISteamUGC_SetItemTitle(ugc, h, main_page["title"].encode("utf-8"))
        api.SteamAPI_ISteamUGC_SetItemDescription(ugc, h, main_page["description"].encode("utf-8"))
        api.SteamAPI_ISteamUGC_SetItemVisibility(ugc, h, VISIBILITY.get(main_page["visibility"], 0))
        api.SteamAPI_ISteamUGC_SetItemContent(ugc, h, contents_dir.encode("utf-8"))
        api.SteamAPI_ISteamUGC_SetItemPreview(ugc, h, preview_png.encode("utf-8"))

        print(f"Uploading content + preview + english page to {item_id}...", flush=True)
        call = api.SteamAPI_ISteamUGC_SubmitItemUpdate(ugc, h, change_note.encode("utf-8"))
        res = wait_call(api, utils, call, SubmitItemUpdateResult, SUBMIT_ITEM_UPDATE_RESULT, "SubmitItemUpdate(english+content)")
        if res.result != 1:
            fail(f"SubmitItemUpdate(english+content) EResult={res.result}")
        print("English + content + preview uploaded OK!", flush=True)

        # 2. Upload localized pages (e.g. workshop_russian.txt)
        for p in sorted(glob.glob(os.path.join(stage, "workshop_*.txt"))):
            lang = os.path.basename(p)[len("workshop_"):-len(".txt")]
            lpage = read_page(p)
            h2 = api.SteamAPI_ISteamUGC_StartItemUpdate(ugc, APP_ID, item_id)
            api.SteamAPI_ISteamUGC_SetItemUpdateLanguage(ugc, h2, lang.encode("utf-8"))
            api.SteamAPI_ISteamUGC_SetItemTitle(ugc, h2, lpage["title"].encode("utf-8"))
            api.SteamAPI_ISteamUGC_SetItemDescription(ugc, h2, lpage["description"].encode("utf-8"))
            call2 = api.SteamAPI_ISteamUGC_SubmitItemUpdate(ugc, h2, b"")
            res2 = wait_call(api, utils, call2, SubmitItemUpdateResult, SUBMIT_ITEM_UPDATE_RESULT, f"SubmitItemUpdate({lang})")
            if res2.result != 1:
                fail(f"SubmitItemUpdate({lang}) EResult={res2.result}")
            print(f"Localized page '{lang}' uploaded OK!", flush=True)

        # 3. Add dependencies from requires.txt
        for dep in read_requires(stage):
            call3 = api.SteamAPI_ISteamUGC_AddDependency(ugc, item_id, dep)
            res3 = wait_call(api, utils, call3, AddUGCDependencyResult, ADD_UGC_DEPENDENCY_RESULT, f"AddDependency({dep})")
            if res3.result not in (1, 29):
                fail(f"AddDependency({dep}) EResult={res3.result}")
            print(f"Dependency {dep} set OK (EResult={res3.result})!", flush=True)

        print(f"ALL DONE: https://steamcommunity.com/sharedfiles/filedetails/?id={item_id}", flush=True)
    finally:
        api.SteamAPI_Shutdown()


if __name__ == "__main__":
    main()
