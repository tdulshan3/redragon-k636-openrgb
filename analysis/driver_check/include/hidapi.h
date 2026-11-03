/* Minimal fake hidapi for checking the K636 controller offline */
#pragma once
#include <cstddef>
#include <cwchar>
struct hid_device_;
typedef struct hid_device_ hid_device;
int  hid_send_feature_report(hid_device* dev, const unsigned char* data, size_t length);
int  hid_get_feature_report(hid_device* dev, unsigned char* data, size_t length);
int  hid_get_serial_number_string(hid_device* dev, wchar_t* string, size_t maxlen);
void hid_close(hid_device* dev);
