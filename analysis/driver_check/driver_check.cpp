/*
 * Offline check of the OpenRGB K636 controller (SinowealthKeyboard49Controller.cpp) against the
 * official app's packets. A fake hidapi records every SET and replays the keyboard's replies from a
 * capture; nothing touches a real device.
 *
 * Every vector runs twice: once with one handle for all reports (Linux, where one hidraw path reaches
 * every collection), and once with one handle per report collection (Windows), where the fake refuses
 * any report sent or read through the wrong collection's handle.
 *
 *   ./build.sh && ./driver_check vectors.txt
 */
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <map>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>
#include "SinowealthKeyboard49Controller.h"
#include "StringUtils.h"

typedef std::vector<unsigned char> bytes;

static std::vector<bytes> sent;
static bytes              probe_reply, config_reply, palette_reply;
static bool               palette_requested;
static bool               split_collections;
static int                wrong_collection;
static std::map<hid_device*, int> closes;

/*------------------------------------------------------------*\
| Fake handles: in the Windows run, handle N reaches only the  |
| collection that declares report N                            |
\*------------------------------------------------------------*/
static hid_device* fake_handle(uintptr_t n) { return reinterpret_cast<hid_device*>(n); }

static bool reachable(hid_device* dev, unsigned char report_id)
{
    if(split_collections && dev != fake_handle(report_id))
    {
        wrong_collection++;
        printf("  report %u went through the handle for report %u\n", report_id, (unsigned)reinterpret_cast<uintptr_t>(dev));
        return false;
    }
    return true;
}

int hid_send_feature_report(hid_device* dev, const unsigned char* data, size_t length)
{
    if(!reachable(dev, data[0]))
    {
        return -1;
    }
    sent.emplace_back(data, data + length);
    if(data[0] == 0x05)
    {
        palette_requested = (data[1] == 0x88);
    }
    return (int)length;
}

int hid_get_feature_report(hid_device* dev, unsigned char* data, size_t length)
{
    if(!reachable(dev, data[0]))
    {
        return -1;
    }
    const bytes& reply = (data[0] == 0x05) ? probe_reply : (palette_requested ? palette_reply : config_reply);
    size_t       n     = std::min(length, reply.size());
    memcpy(data, reply.data(), n);
    return (int)n;
}

int  hid_get_serial_number_string(hid_device*, wchar_t* s, size_t) { s[0] = 0; return 0; }
void hid_close(hid_device* dev) { closes[dev]++; }
std::string StringUtils::wstring_to_string(const std::wstring) { return ""; }

static bytes from_hex(const std::string& hex)
{
    bytes out;
    for(size_t i = 0; i + 1 < hex.size(); i += 2)
    {
        out.push_back((unsigned char)std::stoi(hex.substr(i, 2), nullptr, 16));
    }
    return out;
}

static bool check(const char* what, const std::vector<bytes>& expected)
{
    bool same = (sent == expected);
    printf("%-60s %s\n", what, same ? "identical" : "MISMATCH");
    if(!same)
    {
        for(size_t i = 0; i < std::max(sent.size(), expected.size()); i++)
        {
            printf("  packet %zu: sent %zu bytes, expected %zu bytes\n", i,
                   i < sent.size() ? sent[i].size() : 0, i < expected.size() ? expected[i].size() : 0);
        }
    }
    return same;
}

static bool run_vectors(const char* file, hid_device* cmd_dev, hid_device* data_dev, hid_device* frame_dev)
{
    std::ifstream in(file);
    std::string   line;
    bool          all_ok = true;
    bytes         frame, read_reply, palette_packet;
    std::string   case_line;

    SinowealthKeyboard49Controller controller(cmd_dev, data_dev, frame_dev, "fake", "K636");

    const bytes probe_cmd  = { 0x05, 0x05, 0x81, 0x00, 0x00, 0x00 };
    const bytes config_cmd = { 0x05, 0x83, 0xB6, 0x00, 0x00, 0x00 };

    while(std::getline(in, line))
    {
        std::istringstream ss(line);
        std::string        tag, hex;
        ss >> tag;

        if(tag == "PROBE")
        {
            ss >> hex;
            probe_reply = from_hex(hex);
            sent.clear();
            unsigned char model = SinowealthKeyboard49Controller::ProbeModel(cmd_dev);
            all_ok &= check("probe packet", { probe_cmd });
            printf("%-60s %s\n", "probe reply recognised as K636", model == SINOWEALTH_49_MODEL_K636 ? "yes" : "NO");
            all_ok &= (model == SINOWEALTH_49_MODEL_K636);
        }
        else if(tag == "FRAME")
        {
            ss >> hex;
            frame = from_hex(hex);
            sent.clear();
            controller.SendFrame(bytes(SINOWEALTH_49_FRAME_SIZE, 0x00));
            all_ok &= check("all-zero frame vs app's report 8", { frame });
        }
        else if(tag == "CASE" || tag == "PCASE")
        {
            case_line = line;
            palette_packet.clear();
        }
        else if(tag == "PALETTE")
        {
            ss >> hex;
            palette_packet = from_hex(hex);
        }
        else if(tag == "PALETTEREPLY")
        {
            ss >> hex;
            palette_reply = from_hex(hex);
            sent.clear();
            unsigned char palette[SINOWEALTH_49_PALETTE_BLOCK_SIZE];
            bool          ok = controller.ReadPalette(palette);
            all_ok &= ok && check("palette read packet", { { 0x05, 0x88, 0xB8, 0x00, 0x40, 0x00 } });
            bool same = ok && memcmp(palette, palette_reply.data() + 8, sizeof(palette)) == 0;
            printf("%-60s %s\n", "  palette data taken from byte 8 of the reply", same ? "yes" : "NO");
            all_ok &= same;
        }
        else if(tag == "READ")
        {
            ss >> hex;
            config_reply = from_hex(hex);
        }
        else if(tag == "EXPECT")
        {
            ss >> hex;
            bytes expect = from_hex(hex);

            std::istringstream            cs(case_line);
            std::string                   t;
            double                        time;
            unsigned int                  mode, flag, speed, brightness;
            cs >> t >> time >> mode >> flag >> speed >> brightness;

            sinowealth_49_effect_settings effect = { (unsigned char)flag, (unsigned char)speed, (unsigned char)brightness };

            sent.clear();
            const unsigned char* palette = palette_packet.empty() ? nullptr : palette_packet.data() + 8;
            bool ok = controller.SetMode((unsigned char)mode, mode ? &effect : nullptr, palette);

            char what[128];
            snprintf(what, sizeof(what), "read@%.2f -> SetMode(0x%02X, flag %u, speed %u, bright %u%s)", time, mode, flag, speed, brightness, palette ? ", palette" : "");
            if(palette)
            {
                all_ok &= ok && check(what, { config_cmd, palette_packet, expect });
            }
            else
            {
                all_ok &= ok && check(what, { config_cmd, expect });
            }

            /*------------------------------------------------*\
            | ReadState must decode the same table             |
            \*------------------------------------------------*/
            unsigned char                 state_mode;
            sinowealth_49_effect_settings effects[SINOWEALTH_49_EFFECT_COUNT];
            config_reply = expect;
            config_reply[1] = 0x83;
            controller.ReadState(&state_mode, effects);
            bool decoded = (state_mode == mode) && (mode == 0 || (effects[mode - 1].color_flag == flag && effects[mode - 1].speed == speed && effects[mode - 1].brightness == brightness));
            printf("%-60s %s\n", "  ReadState decodes the written block", decoded ? "yes" : "NO");
            all_ok &= decoded;
        }
    }

    /*--------------------------------------------------------*\
    | A damaged config block must never be written back        |
    \*--------------------------------------------------------*/
    config_reply[0x4E] = 0x00;
    sent.clear();
    bool refused = !controller.SetMode(0x01, nullptr);
    printf("%-60s %s\n", "missing 5a a5 marker: write refused", (refused && sent.size() == 1) ? "yes" : "NO");
    all_ok &= refused && sent.size() == 1;

    return all_ok;
}

int main(int argc, char** argv)
{
    const char* file   = argc > 1 ? argv[1] : "vectors.txt";
    bool        all_ok = true;

    printf("== Linux: one handle reaches every report ==\n");
    split_collections = false;
    wrong_collection  = 0;
    closes.clear();
    all_ok &= run_vectors(file, fake_handle(1), fake_handle(1), fake_handle(1));
    bool closed_once = closes.size() == 1 && closes[fake_handle(1)] == 1;
    printf("%-60s %s\n", "shared handle closed exactly once", closed_once ? "yes" : "NO");
    all_ok &= closed_once;

    printf("== Windows: one handle per report collection ==\n");
    split_collections = true;
    wrong_collection  = 0;
    closes.clear();
    all_ok &= run_vectors(file, fake_handle(5), fake_handle(6), fake_handle(8));
    printf("%-60s %s\n", "every report used its own collection", wrong_collection == 0 ? "yes" : "NO");
    all_ok &= (wrong_collection == 0);
    bool closed_each = closes.size() == 3 && closes[fake_handle(5)] == 1 && closes[fake_handle(6)] == 1 && closes[fake_handle(8)] == 1;
    printf("%-60s %s\n", "each of the three handles closed once", closed_each ? "yes" : "NO");
    all_ok &= closed_each;

    printf("%s\n", all_ok ? "ALL IDENTICAL" : "MISMATCH");
    return all_ok ? 0 : 1;
}
