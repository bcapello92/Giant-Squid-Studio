from typing import Tuple, Iterable
from __future__ import annotations
import sys
import re

PC1:Tuple[int, ...] = (
    (57, 49, 41, 33, 25, 17, 9),
    (1, 58, 50, 42, 34, 26, 18),
    (10, 2, 59, 51, 43, 35, 27),
    (19, 11, 3, 60, 52, 44, 36),
    (63, 55, 47, 39, 31, 23, 15),
    (7, 62, 54, 46, 38, 30 ,22),
    (14, 6, 61, 53, 45, 37, 29),
    (21, 13, 5, 28, 20, 12, 4),   
    )

PC2: Tuple[int, ...] =(
    (14, 17, 11, 24, 1, 5, 3, 28),
    (15, 6, 21, 10, 23 ,19 ,12, 4),
    (26, 8 , 16, 7, 27, 20, 13, 2),
    (41, 52, 31, 37, 47, 55, 30, 40),
    (51, 45, 33, 48, 44, 49, 39, 56),
    (34, 53, 46, 42, 50, 36, 29, 32),
    )

IP: Tuple[int, ...]=(
    (58, 50, 42, 34, 26, 18, 10,2),
    (60, 52, 44, 36, 28, 20, 12, 4),
    (62, 54, 46, 38, 30, 22, 14, 6),
    (64, 56, 48, 40, 32, 24, 16, 8),
    (57, 49, 41, 33, 25, 17, 9, 1),
    (59, 51, 43, 35, 27, 19, 11, 3),
    (61, 53, 45, 37, 29, 21, 13, 5),
    (63, 55, 47, 39, 31, 23, 15, 7),
    )

IP_INV: Tuple[int, ...] =(
    (40, 8, 48, 16, 56, 24, 64, 32),
    (39, 7, 47, 15, 55, 23, 63, 31),
    (38, 6, 46, 14, 54, 22, 62, 30),
    (37, 5, 45, 13, 53, 21, 61, 29),
    (36, 4, 44, 12, 52, 20, 60, 28),
    (35, 3, 43, 11, 51, 19, 59, 27),
    (34, 2, 42, 10, 50, 18 ,58 ,26),
    (33, 1, 41, 9, 49, 17, 57, 25),
    )

E: Tuple[int, ...]=(
    (32, 1, 2, 3, 4, 5),
    (4, 5, 6, 7, 8, 9),
    (8, 9, 10, 11, 12, 13),
    (12, 13, 14, 15, 16, 17),
    (16, 17, 18, 19, 20, 21),
    (20, 21, 22, 23, 24, 25),
    (24, 25, 26, 27, 28, 29),
    (28, 29, 30, 31, 32, 1),    
    )

P: Tuple[int, ...]=(
    (16, 7, 20, 21, 29, 12, 28, 17),
    (1, 15, 23, 26, 5, 18, 31, 10),
    (2, 8, 24, 16, 32, 27, 3, 9),
    (19, 13, 30, 6, 22, 11, 4, 25),
    )

LEFT_SHIFT_SCHEDULE: tuple[tuple[int, int],...]=(
    (1,1),(2,1),(3,2),(4,2),(5,2), (6,2), (7,2), (8,2), (9,1), (10,2), (11,2), (12, 2), (13,2),(14,2),(15,2), (16,1),
    )

SBOXES: Tuple[Tuple[Tuple[int, ...],...],...]=(
    #s1
    (
        (14,4,13,1,2,15,11,8,3,10,6,12,5,9,0,7),
        (0, 15, 7,4,14,2,13,1,10,6,12,11,9,5,3,8),
        (4,1,14,8, 13,6,2,11,15,12,9,7,3,10,5,0),
        (15,12,8,2,4,9,1,7,5,11,3,14, 10,0, 6,13),
    ),
    #s2
    (
        (15,1, 8,14,6, 11,3,4,9,7,2,13,12,0,5,10),
        (3,13,4,7,15,2,8,14,12,0,1,10,6,9,11,5),
        (0,14, 7,11,10,4,13,1,5,8,12,6,9,3,2,15),
        (13,8,10,1,3,15,4,2,11,6,7,12,0,5,14,9),
    ),
    #s3
    (
        (10,0,9,14,6,3,15,5,1,13,12,7,11,4,2,8),
        (13,7,0,9,3,4,6,10,2,8,5,14,12,11,15,1),
        (13,6,4,9,8,15,3,0,11,1,2,12,5,10,14,7),
        (1, 10, 13, 0, 6, 9, 8,7,4,15,14,3,11,5,2,12),
     ),
    #s4
    (
        (7,13,14,3,0,6,9, 10,11,2,8,5,11,12,4,15),
        (13,8,11,5,6,15,0,3,4,7,2, 12, 1,10,14,9),
        (10,6,9,0,12,11,7,13,15,1,3,14,5,2,8,4),
        (3, 15, 0,6,10,1,13,8,9,4,5,11,12,7,2,14),
     ),
    #s5
    (
        (2,12,4,1,7,10,11,6,8,5,3,15,13,0,14,9),
        (14,11,2,12,4,7,13,1,5,0,15,10,3,9,8,6),
        (4,2,1,11,10,13,7,8,15,9,12,5,6,3,0,14),
        (11,8,12,7,1,14,2,13,6,15,0,9,10,4,5,3),
     ),
    #s6
    (
        (12,1,10,15,9,2,6,8,0,13,3,4,14,7,5,11),
        (10,15,4,2,7,12,9,5,6,1,13,14,0, 11,3,8),
        (9,14,15,5,2,8,12,3,7,0,4,10,1,13,11,6),
        (4,3,2,12,9,5,15,10,11,14,1,7,6,0,8,13),
        ),
    #s7
    (
        (4,11,2,14,15,0,8,13,3,12,9,7,5,10,6,1),
        (13,0,11,7,4,9,1,10,14,3,5,12,2,15,8,6),
        (1,4,11,13,12,3,7,14,10,15,6,8,0,5,9,2),
        (6,11,13,8,1,4,10,7,9,5,0,15,14,2,3,12),
        ),
    #s8
    (
        (13, 2,8,4,6,15,11,1,10,9,3,14,5,0,12,7),
        (1, 15, 13, 8,10,3,7,4,12,5,6,11,0,14,9,2),
        (7,11,4,1,9,12,14,2,0,6,10,13,15,3,5,8),
        (2,1,14,7,4,10,8,13,15,12,9,0,3,5,6,11),
        ),
)
def permute(src: int, indices: Iterable[int], src_width: int) -> int:
    """Select bits from src according to 1-based MSB-first indices."""
    out = 0
    for i in indices:
        out = (out << 1) | ((src >> (src_width - i)) & 1)
    return out

def rotl(x: int, k: int, width: int) -> int:
    k %= width
    mask = (1 << width) - 1
    return ((x << k) & mask) | (x >> (width - k))

def split_lr(x: int, width: int) -> Tuple[int, int]:
    half = width // 2
    L = x >> half
    R = x & ((1 << half) - 1)
    return L, R

def sbox8_to_32(x48: int) -> int:
    out32 = 0
    for box in range(8):
        shift = (7 - box) * 6
        chunk = (x48 >> shift) & 0x3F
        row = ((chunk & 0x20) >> 4) | (chunk & 0x01)   # b1b6
        col = (chunk >> 1) & 0xF                       # b2..b5
        val = SBOXES[box][row][col] & 0xF
        out32 = (out32 << 4) | val
    return out32

def feistel(r32: int, k48: int) -> int:
    return permute(sbox8_to_32(permute(r32, E, 32) ^ k48), P, 32)

# --------------------------- Key schedule details ----------------------------

def derive_subkeys_and_CD(key64: int):
    """Return C[0..16], D[0..16], K[1..16]. C[0],D[0] are PC-1 halves."""
    key56 = permute(key64, PC1, 64)
    C0, D0 = split_lr(key56, 56)
    Cs = [C0]
    Ds = [D0]
    Ks = []
    C = C0
    D = D0
    for s in LEFT_SHIFTS:
        C = rotl(C, s, 28)
        D = rotl(D, s, 28)
        Cs.append(C)
        Ds.append(D)
        Ki = permute((C << 28) | D, PC2, 56)
        Ks.append(Ki)
    return Cs, Ds, Ks

# --------------------------- Block encrypt/decrypt ---------------------------

def des_encrypt_block(block64: int, subkeys: Tuple[int, ...]) -> int:
    x = permute(block64, IP, 64)
    L, R = split_lr(x, 64)
    for k in subkeys:
        L, R = R, L ^ feistel(R, k)
    preout = (R << 32) | L
    return permute(preout, IP_INV, 64)

def des_decrypt_block(block64: int, subkeys: Tuple[int, ...]) -> int:
    return des_encrypt_block(block64, tuple(reversed(subkeys)))

# ----------------------------- I/O + printing --------------------------------

def parse_input(path: str):
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()
    # tolerate spaces/case
    def grab(label):
        m = re.search(rf"{label}\s*:\s*([0-9A-Fa-f]+)", text)
        if not m:
            raise ValueError(f"Missing or invalid '{label}:'")
        return m.group(1)

    data_hex = grab("data_block")
    key_hex = grab("key")
    op_m = re.search(r"operation\s*:\s*(encryption|decryption)", text, re.IGNORECASE)
    if not op_m:
        raise ValueError("Missing or invalid 'operation:' (encryption|decryption)")
    operation = op_m.group(1).lower()

    if len(data_hex) != 16 or len(key_hex) != 16:
        raise ValueError("data_block and key must be 16 hex chars (8 bytes).")

    data = int(data_hex, 16)
    key = int(key_hex, 16)
    return data, key, operation

def bstr(x: int, width: int) -> str:
    return format(x, f"0{width}b")

def hstr(x: int, width_bits: int) -> str:
    hex_digits = (width_bits + 3) // 4
    return format(x, f"0{hex_digits}X")

def emit_state(out, Cs, Ds, Ks, Ls, Rs, result_label: str, result64: int):
    write = lambda s: out.write(s + "\n")
    # C0..C16 and D0..D16 (28 bits)
    for i, c in enumerate(Cs):
        write(f"C{i}: {bstr(c, 28)} ({hstr(c, 28)})")
    for i, d in enumerate(Ds):
        write(f"D{i}: {bstr(d, 28)} ({hstr(d, 28)})")

    # K1..K16 (48 bits)
    for i, k in enumerate(Ks, start=1):
        write(f"K{i}: {bstr(k, 48)} ({hstr(k, 48)})")

    # L0..L16 and R0..R16 (32 bits)
    for i, (l, r) in enumerate(zip(Ls, Rs)):
        write(f"L{i}: {bstr(l, 32)} ({hstr(l, 32)})")
        write(f"R{i}: {bstr(r, 32)} ({hstr(r, 32)})")

    write(f"{result_label}: {bstr(result64, 64)} ({hstr(result64, 64)})")

def run(input_path: str, output_path: str):
    data, key, op = parse_input(input_path)

    # Key schedule
    Cs, Ds, Ks = derive_subkeys_and_CD(key)
    subkeys_tuple = tuple(Ks)

    # Data path with L/R capture
    x = permute(data, IP, 64)
    L0, R0 = split_lr(x, 64)
    Ls = [L0]
    Rs = [R0]

    if op == "encryption":
        iter_keys = subkeys_tuple
    else:
        iter_keys = tuple(reversed(subkeys_tuple))

    L, R = L0, R0
    for k in iter_keys:
        L, R = R, L ^ feistel(R, k)
        Ls.append(L)
        Rs.append(R)

    preout = (R << 32) | L
    result64 = permute(preout, IP_INV, 64)
    result_label = "ciphertext" if op == "encryption" else "decrypted_text"

    with open(output_path, "w", encoding="utf-8") as out:
        emit_state(out, Cs, Ds, Ks, Ls, Rs, result_label, result64)

# ---------------------------------- main -------------------------------------

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python DES.py <path to input file> <path to output file>")
        sys.exit(1)
    try:
        run(sys.argv[1], sys.argv[2])
    except Exception as e:
        # Make failures obvious in automated grading
        sys.stderr.write(f"Error: {e}\n")
        sys.exit(2)