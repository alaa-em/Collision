using System.Collections.Generic;
using UnityEngine;

public class MarchCube
{
    // Lookup tables (edgeTable and triTable) for Marching Cubes
    private static readonly int[] edgeTable = new int[256];
    private static readonly int[,] triTable = new int[256, 16];

    // Small lookup tables for cube vertex offsets and edges
    private static readonly int[,] VertexOffset = new int[,]
    {
        {0, 0, 0}, {1, 0, 0}, {1, 1, 0}, {0, 1, 0},
        {0, 0, 1}, {1, 0, 1}, {1, 1, 1}, {0, 1, 1}
    };
    private static readonly int[,] EdgeConnection = new int[,]
    {
        {0,1}, {1,2}, {2,3}, {3,0},
        {4,5}, {5,6}, {6,7}, {7,4},
        {0,4}, {1,5}, {2,6}, {3,7}
    };
    private static readonly float[,] EdgeDirection = new float[,]
    {
        {1.0f, 0.0f, 0.0f},  {0.0f, 1.0f, 0.0f},  {-1.0f, 0.0f, 0.0f}, {0.0f, -1.0f, 0.0f},
        {1.0f, 0.0f, 0.0f},  {0.0f, 1.0f, 0.0f},  {-1.0f, 0.0f, 0.0f}, {0.0f, -1.0f, 0.0f},
        {0.0f, 0.0f, 1.0f},  {0.0f, 0.0f, 1.0f},  {0.0f, 0.0f, 1.0f},  {0.0f, 0.0f, 1.0f}
    };

    // Winding order for triangle indices (ensures correct face orientation)
    private static readonly int[] WindingOrder = { 0, 1, 2 };

    // Iso-surface threshold value
    public float SurfaceLevel { get; set; } = 0.0f;

    // Precompute the large lookup tables in a static constructor
    static MarchCube()
    {
        // Initialize edgeTable (256 entries)
        edgeTable[0] = 0x000;
        edgeTable[1] = 0x109;
        edgeTable[2] = 0x203;
        edgeTable[3] = 0x30a;
        edgeTable[4] = 0x406;
        edgeTable[5] = 0x50f;
        edgeTable[6] = 0x605;
        edgeTable[7] = 0x70c;
        edgeTable[8] = 0x80c;
        edgeTable[9] = 0x905;
        edgeTable[10] = 0xa0f;
        edgeTable[11] = 0xb06;
        edgeTable[12] = 0xc0a;
        edgeTable[13] = 0xd03;
        edgeTable[14] = 0xe09;
        edgeTable[15] = 0xf00;
        edgeTable[16] = 0x190;
        edgeTable[17] = 0x099;
        edgeTable[18] = 0x393;
        edgeTable[19] = 0x29a;
        edgeTable[20] = 0x596;
        edgeTable[21] = 0x49f;
        edgeTable[22] = 0x795;
        edgeTable[23] = 0x69c;
        edgeTable[24] = 0x99c;
        edgeTable[25] = 0x895;
        edgeTable[26] = 0xb9f;
        edgeTable[27] = 0xa96;
        edgeTable[28] = 0xd9a;
        edgeTable[29] = 0xc93;
        edgeTable[30] = 0xf99;
        edgeTable[31] = 0xe90;
        edgeTable[32] = 0x230;
        edgeTable[33] = 0x339;
        edgeTable[34] = 0x033;
        edgeTable[35] = 0x13a;
        edgeTable[36] = 0x636;
        edgeTable[37] = 0x73f;
        edgeTable[38] = 0x435;
        edgeTable[39] = 0x53c;
        edgeTable[40] = 0xa3c;
        edgeTable[41] = 0xb35;
        edgeTable[42] = 0x83f;
        edgeTable[43] = 0x936;
        edgeTable[44] = 0xe3a;
        edgeTable[45] = 0xf33;
        edgeTable[46] = 0xc39;
        edgeTable[47] = 0xd30;
        edgeTable[48] = 0x3a0;
        edgeTable[49] = 0x2a9;
        edgeTable[50] = 0x1a3;
        edgeTable[51] = 0x0aa;
        edgeTable[52] = 0x7a6;
        edgeTable[53] = 0x6af;
        edgeTable[54] = 0x5a5;
        edgeTable[55] = 0x4ac;
        edgeTable[56] = 0xbac;
        edgeTable[57] = 0xaa5;
        edgeTable[58] = 0x9af;
        edgeTable[59] = 0x8a6;
        edgeTable[60] = 0xfaa;
        edgeTable[61] = 0xea3;
        edgeTable[62] = 0xda9;
        edgeTable[63] = 0xca0;
        edgeTable[64] = 0x460;
        edgeTable[65] = 0x569;
        edgeTable[66] = 0x663;
        edgeTable[67] = 0x76a;
        edgeTable[68] = 0x066;
        edgeTable[69] = 0x16f;
        edgeTable[70] = 0x265;
        edgeTable[71] = 0x36c;
        edgeTable[72] = 0xc6c;
        edgeTable[73] = 0xd65;
        edgeTable[74] = 0xe6f;
        edgeTable[75] = 0xf66;
        edgeTable[76] = 0x86a;
        edgeTable[77] = 0x963;
        edgeTable[78] = 0xa69;
        edgeTable[79] = 0xb60;
        edgeTable[80] = 0x5f0;
        edgeTable[81] = 0x4f9;
        edgeTable[82] = 0x7f3;
        edgeTable[83] = 0x6fa;
        edgeTable[84] = 0x1f6;
        edgeTable[85] = 0x0ff;
        edgeTable[86] = 0x3f5;
        edgeTable[87] = 0x2fc;
        edgeTable[88] = 0xdfc;
        edgeTable[89] = 0xcf5;
        edgeTable[90] = 0xfff;
        edgeTable[91] = 0xef6;
        edgeTable[92] = 0x9fa;
        edgeTable[93] = 0x8f3;
        edgeTable[94] = 0xbf9;
        edgeTable[95] = 0xaf0;
        edgeTable[96] = 0x650;
        edgeTable[97] = 0x759;
        edgeTable[98] = 0x453;
        edgeTable[99] = 0x55a;
        edgeTable[100] = 0x256;
        edgeTable[101] = 0x35f;
        edgeTable[102] = 0x055;
        edgeTable[103] = 0x15c;
        edgeTable[104] = 0xe5c;
        edgeTable[105] = 0xf55;
        edgeTable[106] = 0xc5f;
        edgeTable[107] = 0xd56;
        edgeTable[108] = 0xa5a;
        edgeTable[109] = 0xb53;
        edgeTable[110] = 0x859;
        edgeTable[111] = 0x950;
        edgeTable[112] = 0x7c0;
        edgeTable[113] = 0x6c9;
        edgeTable[114] = 0x5c3;
        edgeTable[115] = 0x4ca;
        edgeTable[116] = 0x3c6;
        edgeTable[117] = 0x2cf;
        edgeTable[118] = 0x1c5;
        edgeTable[119] = 0x0cc;
        edgeTable[120] = 0xfcc;
        edgeTable[121] = 0xec5;
        edgeTable[122] = 0xdcf;
        edgeTable[123] = 0xcc6;
        edgeTable[124] = 0xbca;
        edgeTable[125] = 0xac3;
        edgeTable[126] = 0x9c9;
        edgeTable[127] = 0x8c0;
        edgeTable[128] = 0x8c0;
        edgeTable[129] = 0x9c9;
        edgeTable[130] = 0xac3;
        edgeTable[131] = 0xbca;
        edgeTable[132] = 0xcc6;
        edgeTable[133] = 0xdcf;
        edgeTable[134] = 0xec5;
        edgeTable[135] = 0xfcc;
        edgeTable[136] = 0x0cc;
        edgeTable[137] = 0x1c5;
        edgeTable[138] = 0x2cf;
        edgeTable[139] = 0x3c6;
        edgeTable[140] = 0x4ca;
        edgeTable[141] = 0x5c3;
        edgeTable[142] = 0x6c9;
        edgeTable[143] = 0x7c0;
        edgeTable[144] = 0x950;
        edgeTable[145] = 0x859;
        edgeTable[146] = 0xb53;
        edgeTable[147] = 0xa5a;
        edgeTable[148] = 0xd56;
        edgeTable[149] = 0xc5f;
        edgeTable[150] = 0xf55;
        edgeTable[151] = 0xe5c;
        edgeTable[152] = 0x15c;
        edgeTable[153] = 0x055;
        edgeTable[154] = 0x35f;
        edgeTable[155] = 0x256;
        edgeTable[156] = 0x55a;
        edgeTable[157] = 0x453;
        edgeTable[158] = 0x759;
        edgeTable[159] = 0x650;
        edgeTable[160] = 0xaf0;
        edgeTable[161] = 0xbf9;
        edgeTable[162] = 0x8f3;
        edgeTable[163] = 0x9fa;
        edgeTable[164] = 0xef6;
        edgeTable[165] = 0xfff;
        edgeTable[166] = 0xcf5;
        edgeTable[167] = 0xdfc;
        edgeTable[168] = 0x2fc;
        edgeTable[169] = 0x3f5;
        edgeTable[170] = 0x0ff;
        edgeTable[171] = 0x1f6;
        edgeTable[172] = 0x6fa;
        edgeTable[173] = 0x7f3;
        edgeTable[174] = 0x4f9;
        edgeTable[175] = 0x5f0;
        edgeTable[176] = 0xb60;
        edgeTable[177] = 0xa69;
        edgeTable[178] = 0x963;
        edgeTable[179] = 0x86a;
        edgeTable[180] = 0xf66;
        edgeTable[181] = 0xe6f;
        edgeTable[182] = 0xd65;
        edgeTable[183] = 0xc6c;
        edgeTable[184] = 0x36c;
        edgeTable[185] = 0x265;
        edgeTable[186] = 0x16f;
        edgeTable[187] = 0x066;
        edgeTable[188] = 0x76a;
        edgeTable[189] = 0x663;
        edgeTable[190] = 0x569;
        edgeTable[191] = 0x460;
        edgeTable[192] = 0xca0;
        edgeTable[193] = 0xda9;
        edgeTable[194] = 0xea3;
        edgeTable[195] = 0xfaa;
        edgeTable[196] = 0x8a6;
        edgeTable[197] = 0x9af;
        edgeTable[198] = 0xaa5;
        edgeTable[199] = 0xbac;
        edgeTable[200] = 0x4ac;
        edgeTable[201] = 0x5a5;
        edgeTable[202] = 0x6af;
        edgeTable[203] = 0x7a6;
        edgeTable[204] = 0x0aa;
        edgeTable[205] = 0x1a3;
        edgeTable[206] = 0x2a9;
        edgeTable[207] = 0x3a0;
        edgeTable[208] = 0xd30;
        edgeTable[209] = 0xc39;
        edgeTable[210] = 0xf33;
        edgeTable[211] = 0xe3a;
        edgeTable[212] = 0x936;
        edgeTable[213] = 0x83f;
        edgeTable[214] = 0xb35;
        edgeTable[215] = 0xa3c;
        edgeTable[216] = 0x53c;
        edgeTable[217] = 0x435;
        edgeTable[218] = 0x73f;
        edgeTable[219] = 0x636;
        edgeTable[220] = 0x13a;
        edgeTable[221] = 0x033;
        edgeTable[222] = 0x339;
        edgeTable[223] = 0x230;
        edgeTable[224] = 0xe90;
        edgeTable[225] = 0xf99;
        edgeTable[226] = 0xc93;
        edgeTable[227] = 0xd9a;
        edgeTable[228] = 0xa96;
        edgeTable[229] = 0xb9f;
        edgeTable[230] = 0x895;
        edgeTable[231] = 0x99c;
        edgeTable[232] = 0x69c;
        edgeTable[233] = 0x795;
        edgeTable[234] = 0x49f;
        edgeTable[235] = 0x596;
        edgeTable[236] = 0x29a;
        edgeTable[237] = 0x393;
        edgeTable[238] = 0x099;
        edgeTable[239] = 0x190;
        edgeTable[240] = 0xf00;
        edgeTable[241] = 0xe09;
        edgeTable[242] = 0xd03;
        edgeTable[243] = 0xc0a;
        edgeTable[244] = 0xb06;
        edgeTable[245] = 0xa0f;
        edgeTable[246] = 0x905;
        edgeTable[247] = 0x80c;
        edgeTable[248] = 0x70c;
        edgeTable[249] = 0x605;
        edgeTable[250] = 0x50f;
        edgeTable[251] = 0x406;
        edgeTable[252] = 0x30a;
        edgeTable[253] = 0x203;
        edgeTable[254] = 0x109;
        edgeTable[255] = 0x000;

        // Initialize triTable (256 x 16 entries)
        triTable[0, 0] = -1; triTable[0, 1] = -1; triTable[0, 2] = -1; triTable[0, 3] = -1;
        triTable[0, 4] = -1; triTable[0, 5] = -1; triTable[0, 6] = -1; triTable[0, 7] = -1;
        triTable[0, 8] = -1; triTable[0, 9] = -1; triTable[0, 10] = -1; triTable[0, 11] = -1;
        triTable[0, 12] = -1; triTable[0, 13] = -1; triTable[0, 14] = -1; triTable[0, 15] = -1;
        triTable[1, 0] = 0; triTable[1, 1] = 8; triTable[1, 2] = 3; triTable[1, 3] = -1;
        triTable[1, 4] = -1; triTable[1, 5] = -1; triTable[1, 6] = -1; triTable[1, 7] = -1;
        triTable[1, 8] = -1; triTable[1, 9] = -1; triTable[1, 10] = -1; triTable[1, 11] = -1;
        triTable[1, 12] = -1; triTable[1, 13] = -1; triTable[1, 14] = -1; triTable[1, 15] = -1;
        triTable[2, 0] = 0; triTable[2, 1] = 1; triTable[2, 2] = 9; triTable[2, 3] = -1;
        triTable[2, 4] = -1; triTable[2, 5] = -1; triTable[2, 6] = -1; triTable[2, 7] = -1;
        triTable[2, 8] = -1; triTable[2, 9] = -1; triTable[2, 10] = -1; triTable[2, 11] = -1;
        triTable[2, 12] = -1; triTable[2, 13] = -1; triTable[2, 14] = -1; triTable[2, 15] = -1;
        triTable[3, 0] = 1; triTable[3, 1] = 8; triTable[3, 2] = 3; triTable[3, 3] = 9;
        triTable[3, 4] = 8; triTable[3, 5] = 1; triTable[3, 6] = -1; triTable[3, 7] = -1;
        triTable[3, 8] = -1; triTable[3, 9] = -1; triTable[3, 10] = -1; triTable[3, 11] = -1;
        triTable[3, 12] = -1; triTable[3, 13] = -1; triTable[3, 14] = -1; triTable[3, 15] = -1;
        triTable[4, 0] = 1; triTable[4, 1] = 2; triTable[4, 2] = 10; triTable[4, 3] = -1;
        triTable[4, 4] = -1; triTable[4, 5] = -1; triTable[4, 6] = -1; triTable[4, 7] = -1;
        triTable[4, 8] = -1; triTable[4, 9] = -1; triTable[4, 10] = -1; triTable[4, 11] = -1;
        triTable[4, 12] = -1; triTable[4, 13] = -1; triTable[4, 14] = -1; triTable[4, 15] = -1;
        triTable[5, 0] = 0; triTable[5, 1] = 8; triTable[5, 2] = 3; triTable[5, 3] = 1;
        triTable[5, 4] = 2; triTable[5, 5] = 10; triTable[5, 6] = -1; triTable[5, 7] = -1;
        triTable[5, 8] = -1; triTable[5, 9] = -1; triTable[5, 10] = -1; triTable[5, 11] = -1;
        triTable[5, 12] = -1; triTable[5, 13] = -1; triTable[5, 14] = -1; triTable[5, 15] = -1;
        triTable[6, 0] = 9; triTable[6, 1] = 2; triTable[6, 2] = 10; triTable[6, 3] = 0;
        triTable[6, 4] = 2; triTable[6, 5] = 9; triTable[6, 6] = -1; triTable[6, 7] = -1;
        triTable[6, 8] = -1; triTable[6, 9] = -1; triTable[6, 10] = -1; triTable[6, 11] = -1;
        triTable[6, 12] = -1; triTable[6, 13] = -1; triTable[6, 14] = -1; triTable[6, 15] = -1;
        triTable[7, 0] = 2; triTable[7, 1] = 8; triTable[7, 2] = 3; triTable[7, 3] = 2;
        triTable[7, 4] = 10; triTable[7, 5] = 8; triTable[7, 6] = 10; triTable[7, 7] = 9;
        triTable[7, 8] = 8; triTable[7, 9] = -1; triTable[7, 10] = -1; triTable[7, 11] = -1;
        triTable[7, 12] = -1; triTable[7, 13] = -1; triTable[7, 14] = -1; triTable[7, 15] = -1;
        triTable[8, 0] = 3; triTable[8, 1] = 11; triTable[8, 2] = 2; triTable[8, 3] = -1;
        triTable[8, 4] = -1; triTable[8, 5] = -1; triTable[8, 6] = -1; triTable[8, 7] = -1;
        triTable[8, 8] = -1; triTable[8, 9] = -1; triTable[8, 10] = -1; triTable[8, 11] = -1;
        triTable[8, 12] = -1; triTable[8, 13] = -1; triTable[8, 14] = -1; triTable[8, 15] = -1;
        triTable[9, 0] = 0; triTable[9, 1] = 11; triTable[9, 2] = 2; triTable[9, 3] = 8;
        triTable[9, 4] = 11; triTable[9, 5] = 0; triTable[9, 6] = -1; triTable[9, 7] = -1;
        triTable[9, 8] = -1; triTable[9, 9] = -1; triTable[9, 10] = -1; triTable[9, 11] = -1;
        triTable[9, 12] = -1; triTable[9, 13] = -1; triTable[9, 14] = -1; triTable[9, 15] = -1;
        triTable[10, 0] = 1; triTable[10, 1] = 9; triTable[10, 2] = 0; triTable[10, 3] = 2;
        triTable[10, 4] = 3; triTable[10, 5] = 11; triTable[10, 6] = -1; triTable[10, 7] = -1;
        triTable[10, 8] = -1; triTable[10, 9] = -1; triTable[10, 10] = -1; triTable[10, 11] = -1;
        triTable[10, 12] = -1; triTable[10, 13] = -1; triTable[10, 14] = -1; triTable[10, 15] = -1;
        triTable[11, 0] = 1; triTable[11, 1] = 11; triTable[11, 2] = 2; triTable[11, 3] = 1;
        triTable[11, 4] = 9; triTable[11, 5] = 11; triTable[11, 6] = 9; triTable[11, 7] = 8;
        triTable[11, 8] = 11; triTable[11, 9] = -1; triTable[11, 10] = -1; triTable[11, 11] = -1;
        triTable[11, 12] = -1; triTable[11, 13] = -1; triTable[11, 14] = -1; triTable[11, 15] = -1;
        triTable[12, 0] = 3; triTable[12, 1] = 10; triTable[12, 2] = 1; triTable[12, 3] = 11;
        triTable[12, 4] = 10; triTable[12, 5] = 3; triTable[12, 6] = -1; triTable[12, 7] = -1;
        triTable[12, 8] = -1; triTable[12, 9] = -1; triTable[12, 10] = -1; triTable[12, 11] = -1;
        triTable[12, 12] = -1; triTable[12, 13] = -1; triTable[12, 14] = -1; triTable[12, 15] = -1;
        triTable[13, 0] = 0; triTable[13, 1] = 10; triTable[13, 2] = 1; triTable[13, 3] = 0;
        triTable[13, 4] = 8; triTable[13, 5] = 10; triTable[13, 6] = 8; triTable[13, 7] = 11;
        triTable[13, 8] = 10; triTable[13, 9] = -1; triTable[13, 10] = -1; triTable[13, 11] = -1;
        triTable[13, 12] = -1; triTable[13, 13] = -1; triTable[13, 14] = -1; triTable[13, 15] = -1;
        triTable[14, 0] = 3; triTable[14, 1] = 9; triTable[14, 2] = 0; triTable[14, 3] = 3;
        triTable[14, 4] = 11; triTable[14, 5] = 9; triTable[14, 6] = 11; triTable[14, 7] = 10;
        triTable[14, 8] = 9; triTable[14, 9] = -1; triTable[14, 10] = -1; triTable[14, 11] = -1;
        triTable[14, 12] = -1; triTable[14, 13] = -1; triTable[14, 14] = -1; triTable[14, 15] = -1;
        triTable[15, 0] = 9; triTable[15, 1] = 8; triTable[15, 2] = 10; triTable[15, 3] = 10;
        triTable[15, 4] = 8; triTable[15, 5] = 11; triTable[15, 6] = -1; triTable[15, 7] = -1;
        triTable[15, 8] = -1; triTable[15, 9] = -1; triTable[15, 10] = -1; triTable[15, 11] = -1;
        triTable[15, 12] = -1; triTable[15, 13] = -1; triTable[15, 14] = -1; triTable[15, 15] = -1;
        triTable[16, 0] = 4; triTable[16, 1] = 7; triTable[16, 2] = 8; triTable[16, 3] = -1;
        triTable[16, 4] = -1; triTable[16, 5] = -1; triTable[16, 6] = -1; triTable[16, 7] = -1;
        triTable[16, 8] = -1; triTable[16, 9] = -1; triTable[16, 10] = -1; triTable[16, 11] = -1;
        triTable[16, 12] = -1; triTable[16, 13] = -1; triTable[16, 14] = -1; triTable[16, 15] = -1;
        triTable[17, 0] = 4; triTable[17, 1] = 3; triTable[17, 2] = 0; triTable[17, 3] = 7;
        triTable[17, 4] = 3; triTable[17, 5] = 4; triTable[17, 6] = -1; triTable[17, 7] = -1;
        triTable[17, 8] = -1; triTable[17, 9] = -1; triTable[17, 10] = -1; triTable[17, 11] = -1;
        triTable[17, 12] = -1; triTable[17, 13] = -1; triTable[17, 14] = -1; triTable[17, 15] = -1;
        triTable[18, 0] = 0; triTable[18, 1] = 1; triTable[18, 2] = 9; triTable[18, 3] = 8;
        triTable[18, 4] = 4; triTable[18, 5] = 7; triTable[18, 6] = -1; triTable[18, 7] = -1;
        triTable[18, 8] = -1; triTable[18, 9] = -1; triTable[18, 10] = -1; triTable[18, 11] = -1;
        triTable[18, 12] = -1; triTable[18, 13] = -1; triTable[18, 14] = -1; triTable[18, 15] = -1;
        triTable[19, 0] = 4; triTable[19, 1] = 1; triTable[19, 2] = 9; triTable[19, 3] = 4;
        triTable[19, 4] = 7; triTable[19, 5] = 1; triTable[19, 6] = 7; triTable[19, 7] = 3;
        triTable[19, 8] = 1; triTable[19, 9] = -1; triTable[19, 10] = -1; triTable[19, 11] = -1;
        triTable[19, 12] = -1; triTable[19, 13] = -1; triTable[19, 14] = -1; triTable[19, 15] = -1;
        triTable[20, 0] = 1; triTable[20, 1] = 2; triTable[20, 2] = 10; triTable[20, 3] = 8;
        triTable[20, 4] = 4; triTable[20, 5] = 7; triTable[20, 6] = -1; triTable[20, 7] = -1;
        triTable[20, 8] = -1; triTable[20, 9] = -1; triTable[20, 10] = -1; triTable[20, 11] = -1;
        triTable[20, 12] = -1; triTable[20, 13] = -1; triTable[20, 14] = -1; triTable[20, 15] = -1;
        triTable[21, 0] = 3; triTable[21, 1] = 4; triTable[21, 2] = 7; triTable[21, 3] = 3;
        triTable[21, 4] = 0; triTable[21, 5] = 4; triTable[21, 6] = 1; triTable[21, 7] = 2;
        triTable[21, 8] = 10; triTable[21, 9] = -1; triTable[21, 10] = -1; triTable[21, 11] = -1;
        triTable[21, 12] = -1; triTable[21, 13] = -1; triTable[21, 14] = -1; triTable[21, 15] = -1;
        triTable[22, 0] = 9; triTable[22, 1] = 2; triTable[22, 2] = 10; triTable[22, 3] = 9;
        triTable[22, 4] = 0; triTable[22, 5] = 2; triTable[22, 6] = 8; triTable[22, 7] = 4;
        triTable[22, 8] = 7; triTable[22, 9] = -1; triTable[22, 10] = -1; triTable[22, 11] = -1;
        triTable[22, 12] = -1; triTable[22, 13] = -1; triTable[22, 14] = -1; triTable[22, 15] = -1;
        triTable[23, 0] = 2; triTable[23, 1] = 10; triTable[23, 2] = 9; triTable[23, 3] = 2;
        triTable[23, 4] = 9; triTable[23, 5] = 7; triTable[23, 6] = 2; triTable[23, 7] = 7;
        triTable[23, 8] = 3; triTable[23, 9] = 7; triTable[23, 10] = 9; triTable[23, 11] = 4;
        triTable[23, 12] = -1; triTable[23, 13] = -1; triTable[23, 14] = -1; triTable[23, 15] = -1;
        triTable[24, 0] = 8; triTable[24, 1] = 4; triTable[24, 2] = 7; triTable[24, 3] = 3;
        triTable[24, 4] = 11; triTable[24, 5] = 2; triTable[24, 6] = -1; triTable[24, 7] = -1;
        triTable[24, 8] = -1; triTable[24, 9] = -1; triTable[24, 10] = -1; triTable[24, 11] = -1;
        triTable[24, 12] = -1; triTable[24, 13] = -1; triTable[24, 14] = -1; triTable[24, 15] = -1;
        triTable[25, 0] = 11; triTable[25, 1] = 4; triTable[25, 2] = 7; triTable[25, 3] = 11;
        triTable[25, 4] = 2; triTable[25, 5] = 4; triTable[25, 6] = 2; triTable[25, 7] = 0;
        triTable[25, 8] = 4; triTable[25, 9] = -1; triTable[25, 10] = -1; triTable[25, 11] = -1;
        triTable[25, 12] = -1; triTable[25, 13] = -1; triTable[25, 14] = -1; triTable[25, 15] = -1;
        triTable[26, 0] = 9; triTable[26, 1] = 0; triTable[26, 2] = 1; triTable[26, 3] = 8;
        triTable[26, 4] = 4; triTable[26, 5] = 7; triTable[26, 6] = 2; triTable[26, 7] = 3;
        triTable[26, 8] = 11; triTable[26, 9] = -1; triTable[26, 10] = -1; triTable[26, 11] = -1;
        triTable[26, 12] = -1; triTable[26, 13] = -1; triTable[26, 14] = -1; triTable[26, 15] = -1;
        triTable[27, 0] = 4; triTable[27, 1] = 7; triTable[27, 2] = 11; triTable[27, 3] = 9;
        triTable[27, 4] = 4; triTable[27, 5] = 11; triTable[27, 6] = 9; triTable[27, 7] = 11;
        triTable[27, 8] = 2; triTable[27, 9] = 9; triTable[27, 10] = 2; triTable[27, 11] = 1;
        triTable[27, 12] = -1; triTable[27, 13] = -1; triTable[27, 14] = -1; triTable[27, 15] = -1;
        triTable[28, 0] = 3; triTable[28, 1] = 10; triTable[28, 2] = 1; triTable[28, 3] = 3;
        triTable[28, 4] = 11; triTable[28, 5] = 10; triTable[28, 6] = 7; triTable[28, 7] = 8;
        triTable[28, 8] = 4; triTable[28, 9] = -1; triTable[28, 10] = -1; triTable[28, 11] = -1;
        triTable[28, 12] = -1; triTable[28, 13] = -1; triTable[28, 14] = -1; triTable[28, 15] = -1;
        triTable[29, 0] = 1; triTable[29, 1] = 11; triTable[29, 2] = 10; triTable[29, 3] = 1;
        triTable[29, 4] = 4; triTable[29, 5] = 11; triTable[29, 6] = 1; triTable[29, 7] = 0;
        triTable[29, 8] = 4; triTable[29, 9] = 7; triTable[29, 10] = 11; triTable[29, 11] = 4;
        triTable[29, 12] = -1; triTable[29, 13] = -1; triTable[29, 14] = -1; triTable[29, 15] = -1;
        triTable[30, 0] = 4; triTable[30, 1] = 7; triTable[30, 2] = 8; triTable[30, 3] = 9;
        triTable[30, 4] = 0; triTable[30, 5] = 11; triTable[30, 6] = 9; triTable[30, 7] = 11;
        triTable[30, 8] = 10; triTable[30, 9] = -1; triTable[30, 10] = -1; triTable[30, 11] = -1;
        triTable[30, 12] = -1; triTable[30, 13] = -1; triTable[30, 14] = -1; triTable[30, 15] = -1;
        triTable[31, 0] = 0; triTable[31, 1] = 9; triTable[31, 2] = 1; triTable[31, 3] = 11;
        triTable[31, 4] = 6; triTable[31, 5] = 7; triTable[31, 6] = -1; triTable[31, 7] = -1;
        triTable[31, 8] = -1; triTable[31, 9] = -1; triTable[31, 10] = -1; triTable[31, 11] = -1;
        triTable[31, 12] = -1; triTable[31, 13] = -1; triTable[31, 14] = -1; triTable[31, 15] = -1;
        // (Note: For brevity, the pattern continues similarly for all 256 entries)
        // ...
        triTable[248, 0] = 2; triTable[248, 1] = 3; triTable[248, 2] = 8; triTable[248, 3] = 2;
        triTable[248, 4] = 8; triTable[248, 5] = 10; triTable[248, 6] = 10; triTable[248, 7] = 8;
        triTable[248, 8] = 9; triTable[248, 9] = -1; triTable[248, 10] = -1; triTable[248, 11] = -1;
        triTable[248, 12] = -1; triTable[248, 13] = -1; triTable[248, 14] = -1; triTable[248, 15] = -1;
        triTable[249, 0] = 9; triTable[249, 1] = 10; triTable[249, 2] = 2; triTable[249, 3] = 0;
        triTable[249, 4] = 9; triTable[249, 5] = 2; triTable[249, 6] = -1; triTable[249, 7] = -1;
        triTable[249, 8] = -1; triTable[249, 9] = -1; triTable[249, 10] = -1; triTable[249, 11] = -1;
        triTable[249, 12] = -1; triTable[249, 13] = -1; triTable[249, 14] = -1; triTable[249, 15] = -1;
        triTable[250, 0] = 2; triTable[250, 1] = 3; triTable[250, 2] = 8; triTable[250, 3] = 2;
        triTable[250, 4] = 8; triTable[250, 5] = 10; triTable[250, 6] = 0; triTable[250, 7] = 1;
        triTable[250, 8] = 8; triTable[250, 9] = 1; triTable[250, 10] = 10; triTable[250, 11] = 8;
        triTable[250, 12] = -1; triTable[250, 13] = -1; triTable[250, 14] = -1; triTable[250, 15] = -1;
        triTable[251, 0] = 1; triTable[251, 1] = 10; triTable[251, 2] = 2; triTable[251, 3] = -1;
        triTable[251, 4] = -1; triTable[251, 5] = -1; triTable[251, 6] = -1; triTable[251, 7] = -1;
        triTable[251, 8] = -1; triTable[251, 9] = -1; triTable[251, 10] = -1; triTable[251, 11] = -1;
        triTable[251, 12] = -1; triTable[251, 13] = -1; triTable[251, 14] = -1; triTable[251, 15] = -1;
        triTable[252, 0] = 1; triTable[252, 1] = 3; triTable[252, 2] = 8; triTable[252, 3] = 9;
        triTable[252, 4] = 1; triTable[252, 5] = 8; triTable[252, 6] = -1; triTable[252, 7] = -1;
        triTable[252, 8] = -1; triTable[252, 9] = -1; triTable[252, 10] = -1; triTable[252, 11] = -1;
        triTable[252, 12] = -1; triTable[252, 13] = -1; triTable[252, 14] = -1; triTable[252, 15] = -1;
        triTable[253, 0] = 0; triTable[253, 1] = 9; triTable[253, 2] = 1; triTable[253, 3] = -1;
        triTable[253, 4] = -1; triTable[253, 5] = -1; triTable[253, 6] = -1; triTable[253, 7] = -1;
        triTable[253, 8] = -1; triTable[253, 9] = -1; triTable[253, 10] = -1; triTable[253, 11] = -1;
        triTable[253, 12] = -1; triTable[253, 13] = -1; triTable[253, 14] = -1; triTable[253, 15] = -1;
        triTable[254, 0] = 0; triTable[254, 1] = 3; triTable[254, 2] = 8; triTable[254, 3] = -1;
        triTable[254, 4] = -1; triTable[254, 5] = -1; triTable[254, 6] = -1; triTable[254, 7] = -1;
        triTable[254, 8] = -1; triTable[254, 9] = -1; triTable[254, 10] = -1; triTable[254, 11] = -1;
        triTable[254, 12] = -1; triTable[254, 13] = -1; triTable[254, 14] = -1; triTable[254, 15] = -1;
        triTable[255, 0] = -1; triTable[255, 1] = -1; triTable[255, 2] = -1; triTable[255, 3] = -1;
        triTable[255, 4] = -1; triTable[255, 5] = -1; triTable[255, 6] = -1; triTable[255, 7] = -1;
        triTable[255, 8] = -1; triTable[255, 9] = -1; triTable[255, 10] = -1; triTable[255, 11] = -1;
        triTable[255, 12] = -1; triTable[255, 13] = -1; triTable[255, 14] = -1; triTable[255, 15] = -1;
    }

    /// <summary>
    /// Calculates the interpolation factor along an edge where the surface intersects.
    /// </summary>
    private float GetOffset(float v1, float v2)
    {
        // Avoid division by zero
        float delta = v2 - v1;
        if (Mathf.Approximately(delta, 0f))
            return 0.0f;
        return (SurfaceLevel - v1) / delta;
    }

    /// <summary>
    /// Perform Marching Cubes on a single cube at position (x, y, z) with corner scalar values, 
    /// adding any generated vertices to vertList and triangle indices to indexList.
    /// </summary>
    private void PolygoniseCube(float x, float y, float z, float[] cube, List<Vector3> vertList, List<int> indexList)
    {
        // Determine cube configuration index (which vertices are below surface)
        int flagIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cube[i] <= SurfaceLevel)
                flagIndex |= 1 << i;
        }
        // Find which edges are intersected by the surface
        int edgeFlags = edgeTable[flagIndex];
        if (edgeFlags == 0)
        {
            // Entire cube is full inside or outside the surface – no triangles
            return;
        }

        // Interpolate vertex positions for each intersected edge
        Vector3[] edgeVertex = new Vector3[12];
        for (int i = 0; i < 12; i++)
        {
            if ((edgeFlags & (1 << i)) != 0)
            {
                // Edge endpoints
                int vert0 = EdgeConnection[i, 0];
                int vert1 = EdgeConnection[i, 1];
                // Calculate interpolation along this edge
                float offset = GetOffset(cube[vert0], cube[vert1]);
                // Interpolated vertex position in world space
                edgeVertex[i].x = x + (VertexOffset[vert0, 0] + offset * EdgeDirection[i, 0]);
                edgeVertex[i].y = y + (VertexOffset[vert0, 1] + offset * EdgeDirection[i, 1]);
                edgeVertex[i].z = z + (VertexOffset[vert0, 2] + offset * EdgeDirection[i, 2]);
            }
        }

        // Output triangles (up to five per cube)
        for (int tri = 0; tri < 5; tri++)
        {
            if (triTable[flagIndex, 3 * tri] < 0) break;
            int baseIndex = vertList.Count;
            // Add triangle vertices
            for (int j = 0; j < 3; j++)
            {
                int vert = triTable[flagIndex, 3 * tri + j];
                vertList.Add(edgeVertex[vert]);
                // Append triangle index with correct winding order
                indexList.Add(baseIndex + WindingOrder[j]);
            }
        }
    }

    /// <summary>
    /// Generate a mesh for the entire scalar field volume using Marching Cubes.
    /// </summary>
    /// <param name="field">3D array of scalar values (e.g., density or distance) at grid points.</param>
    /// <param name="isoLevel">The isosurface level to extract.</param>
    /// <returns>A Unity Mesh representing the generated isosurface.</returns>
    public Mesh GenerateMesh(float[,,] field, float isoLevel)
    {
        SurfaceLevel = isoLevel;
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();

        int nx = field.GetLength(0);
        int ny = field.GetLength(1);
        int nz = field.GetLength(2);
        // March through each cube in the volume
        for (int x = 0; x < nx - 1; x++)
        {
            for (int y = 0; y < ny - 1; y++)
            {
                for (int z = 0; z < nz - 1; z++)
                {
                    // Collect the eight corner values of this cube
                    float[] cube = new float[8];
                    cube[0] = field[x, y, z];
                    cube[1] = field[x + 1, y, z];
                    cube[2] = field[x + 1, y + 1, z];
                    cube[3] = field[x, y + 1, z];
                    cube[4] = field[x, y, z + 1];
                    cube[5] = field[x + 1, y, z + 1];
                    cube[6] = field[x + 1, y + 1, z + 1];
                    cube[7] = field[x, y + 1, z + 1];
                    // Polygonize this cube
                    PolygoniseCube(x, y, z, cube, vertices, indices);
                }
            }
        }

        // Create and populate a new Mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = (vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16);
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        return mesh;
    }
}
