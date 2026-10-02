#!/bin/fish

set -eu

if [ ! -f ./disk.qcow2 ]; then
    qemu-img create -f qcow2 disk.qcow2 32G
fi
websockify -D --web=/usr/share/novnc/ 6080 127.0.0.1:5900 &

qemu-system-x86_64 -boot order=cd -machine q35 -name PrimaryVM -device qemu-xhci -device usb-tablet -m 4G -smp "$(nproc)" \
-cpu max -accel tcg,thread=multi -cdrom VM1.iso -drive -hda disk.qcow2,index=0,format=qcow2,if=virtio -display none \
-vnc 127.0.0.1:0 -vga virtio