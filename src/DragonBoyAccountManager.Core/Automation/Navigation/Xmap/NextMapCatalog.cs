namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public static class NextMapCatalog
{
    private static readonly Lock Lock = new();
    public static readonly Dictionary<int, NextMapData> Maps = new()
    {
        [0] = new NextMapData
        {
            Id = 0,
            Name = "Làng Aru",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(42, MapDirection.Left),
                NextMapAdjacent.Waypoint(21, MapDirection.Center),
                NextMapAdjacent.Waypoint(1, MapDirection.Right),
                NextMapAdjacent.Npc(149, 67, 3, [0]),
            ]
        },
        [1] = new NextMapData
        {
            Id = 1,
            Name = "Đồi hoa cúc",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(0, MapDirection.Left),
                NextMapAdjacent.Waypoint(47, MapDirection.Center),
                NextMapAdjacent.Waypoint(2, MapDirection.Right),
            ]
        },
        [2] = new NextMapData
        {
            Id = 2,
            Name = "Thung lũng tre",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(1, MapDirection.Left),
                NextMapAdjacent.Waypoint(24, MapDirection.Center),
                NextMapAdjacent.Waypoint(3, MapDirection.Right),
            ]
        },
        [3] = new NextMapData
        {
            Id = 3,
            Name = "Rừng nấm",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(2, MapDirection.Left),
                NextMapAdjacent.Waypoint(27, MapDirection.Center),
                NextMapAdjacent.Waypoint(4, MapDirection.Right),
            ]
        },
        [4] = new NextMapData
        {
            Id = 4,
            Name = "Rừng xương",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(3, MapDirection.Left),
                NextMapAdjacent.Waypoint(5, MapDirection.Right),
            ]
        },
        [5] = new NextMapData
        {
            Id = 5,
            Name = "Đảo Kamê",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(4, MapDirection.Left),
                NextMapAdjacent.Waypoint(29, MapDirection.Center),
                NextMapAdjacent.Waypoint(6, MapDirection.Right),
                NextMapAdjacent.Npc(153, 13, 0),
            ]
        },
        [6] = new NextMapData
        {
            Id = 6,
            Name = "Đông Karin",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(5, MapDirection.Left),
            ]
        },
        [7] = new NextMapData
        {
            Id = 7,
            Name = "Làng Mori",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(43, MapDirection.Left),
                NextMapAdjacent.Waypoint(22, MapDirection.Center),
                NextMapAdjacent.Waypoint(8, MapDirection.Right),
            ]
        },
        [8] = new NextMapData
        {
            Id = 8,
            Name = "Đồi nấm tím",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(7, MapDirection.Left),
                NextMapAdjacent.Waypoint(9, MapDirection.Right),
            ]
        },
        [9] = new NextMapData
        {
            Id = 9,
            Name = "Thị trấn Moori",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(8, MapDirection.Left),
                NextMapAdjacent.Waypoint(25, MapDirection.Center),
                NextMapAdjacent.Waypoint(11, MapDirection.Right),
            ]
        },
        [10] = new NextMapData
        {
            Id = 10,
            Name = "Thung lũng Namếc",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(13, MapDirection.Left),
            ]
        },
        [11] = new NextMapData
        {
            Id = 11,
            Name = "Thung lũng Maima",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(9, MapDirection.Left),
                NextMapAdjacent.Waypoint(31, MapDirection.Center),
                NextMapAdjacent.Waypoint(12, MapDirection.Right),
            ]
        },
        [12] = new NextMapData
        {
            Id = 12,
            Name = "Vực maima",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(11, MapDirection.Left),
                NextMapAdjacent.Waypoint(13, MapDirection.Right),
            ]
        },
        [13] = new NextMapData
        {
            Id = 13,
            Name = "Đảo Guru",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(12, MapDirection.Left),
                NextMapAdjacent.Waypoint(33, MapDirection.Center),
                NextMapAdjacent.Waypoint(10, MapDirection.Right),
            ]
        },
        [14] = new NextMapData
        {
            Id = 14,
            Name = "Làng Kakarot",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(44, MapDirection.Left),
                NextMapAdjacent.Waypoint(23, MapDirection.Center),
                NextMapAdjacent.Waypoint(15, MapDirection.Right),
            ]
        },
        [15] = new NextMapData
        {
            Id = 15,
            Name = "Đồi hoang",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(14, MapDirection.Left),
                NextMapAdjacent.Waypoint(16, MapDirection.Right),
            ]
        },
        [16] = new NextMapData
        {
            Id = 16,
            Name = "Làng Plant",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(15, MapDirection.Left),
                NextMapAdjacent.Waypoint(26, MapDirection.Center),
                NextMapAdjacent.Waypoint(17, MapDirection.Right),
            ]
        },
        [17] = new NextMapData
        {
            Id = 17,
            Name = "Rừng nguyên sinh",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(16, MapDirection.Left),
                NextMapAdjacent.Waypoint(35, MapDirection.Center),
                NextMapAdjacent.Waypoint(18, MapDirection.Right),
            ]
        },
        [18] = new NextMapData
        {
            Id = 18,
            Name = "Rừng thông Xayda",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(17, MapDirection.Left),
                NextMapAdjacent.Waypoint(20, MapDirection.Right),
            ]
        },
        [19] = new NextMapData
        {
            Id = 19,
            Name = "Thành phố Vegeta",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(20, MapDirection.Left),
                NextMapAdjacent.Npc(68, 12, 1),
                NextMapAdjacent.Npc(126, 53, 0),
                NextMapAdjacent.Npc(109, 12, 0),
            ]
        },
        [20] = new NextMapData
        {
            Id = 20,
            Name = "Vách núi đen",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(18, MapDirection.Left),
                NextMapAdjacent.Waypoint(37, MapDirection.Center),
                NextMapAdjacent.Waypoint(19, MapDirection.Right),
            ]
        },
        [21] = new NextMapData
        {
            Id = 21,
            Name = "Nhà Gôhan",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(0, MapDirection.Right),
            ]
        },
        [22] = new NextMapData
        {
            Id = 22,
            Name = "Nhà Moori",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(7, MapDirection.Right),
            ]
        },
        [23] = new NextMapData
        {
            Id = 23,
            Name = "Nhà Broly",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(14, MapDirection.Right),
            ]
        },
        [24] = new NextMapData
        {
            Id = 24,
            Name = "Trạm tàu T.Đất",
            PlanetId = 0,
            IsSpaceshipStation = true,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(2, MapDirection.Left),
                NextMapAdjacent.Npc(25, 10, 0),
                NextMapAdjacent.Npc(26, 10, 1),
                NextMapAdjacent.Npc(84, 10, 2),
                NextMapAdjacent.Npc(139, 63, 0),
                NextMapAdjacent.Npc(85, 29, 0),
                NextMapAdjacent.Npc(86, 29, 1),
                NextMapAdjacent.Npc(87, 29, 2),
                NextMapAdjacent.Npc(88, 29, 3),
                NextMapAdjacent.Npc(89, 29, 4),
                NextMapAdjacent.Npc(90, 29, 5),
                NextMapAdjacent.Npc(91, 29, 6),
            ]
        },
        [25] = new NextMapData
        {
            Id = 25,
            Name = "Trạm tàu Namếc",
            PlanetId = 1,
            IsSpaceshipStation = true,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(9, MapDirection.Left),
                NextMapAdjacent.Npc(24, 11, 0),
                NextMapAdjacent.Npc(26, 11, 1),
                NextMapAdjacent.Npc(84, 11, 2),
                NextMapAdjacent.Npc(85, 29, 0),
                NextMapAdjacent.Npc(86, 29, 1),
                NextMapAdjacent.Npc(87, 29, 2),
                NextMapAdjacent.Npc(88, 29, 3),
                NextMapAdjacent.Npc(89, 29, 4),
                NextMapAdjacent.Npc(90, 29, 5),
                NextMapAdjacent.Npc(91, 29, 6),
            ]
        },
        [26] = new NextMapData
        {
            Id = 26,
            Name = "Trạm tàu Xayda",
            PlanetId = 2,
            IsSpaceshipStation = true,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(16, MapDirection.Left),
                NextMapAdjacent.Npc(24, 12, 0),
                NextMapAdjacent.Npc(25, 12, 1),
                NextMapAdjacent.Npc(84, 12, 2),
                NextMapAdjacent.Npc(85, 29, 0),
                NextMapAdjacent.Npc(86, 29, 1),
                NextMapAdjacent.Npc(87, 29, 2),
                NextMapAdjacent.Npc(88, 29, 3),
                NextMapAdjacent.Npc(89, 29, 4),
                NextMapAdjacent.Npc(90, 29, 5),
                NextMapAdjacent.Npc(91, 29, 6),
            ]
        },
        [27] = new NextMapData
        {
            Id = 27,
            Name = "Rừng Bamboo",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(3, MapDirection.Left),
                NextMapAdjacent.Waypoint(28, MapDirection.Right),
                NextMapAdjacent.Npc(53, 25, 0),
                NextMapAdjacent.Npc(102, 38, 1),
            ]
        },
        [28] = new NextMapData
        {
            Id = 28,
            Name = "Rừng dương xỉ",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(27, MapDirection.Left),
                NextMapAdjacent.Waypoint(29, MapDirection.Right),
                NextMapAdjacent.Npc(102, 38, 1),
            ]
        },
        [29] = new NextMapData
        {
            Id = 29,
            Name = "Nam Kamê",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(28, MapDirection.Left),
                NextMapAdjacent.Waypoint(5, MapDirection.Center),
                NextMapAdjacent.Waypoint(30, MapDirection.Right),
                NextMapAdjacent.Npc(102, 38, 1),
            ]
        },
        [30] = new NextMapData
        {
            Id = 30,
            Name = "Đảo Bulông",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(29, MapDirection.Left),
            ]
        },
        [31] = new NextMapData
        {
            Id = 31,
            Name = "Núi hoa vàng",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(11, MapDirection.Left),
                NextMapAdjacent.Waypoint(32, MapDirection.Right),
            ]
        },
        [32] = new NextMapData
        {
            Id = 32,
            Name = "Núi hoa tím",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(31, MapDirection.Left),
                NextMapAdjacent.Waypoint(33, MapDirection.Right),
            ]
        },
        [33] = new NextMapData
        {
            Id = 33,
            Name = "Nam Guru",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(32, MapDirection.Left),
                NextMapAdjacent.Waypoint(13, MapDirection.Center),
                NextMapAdjacent.Waypoint(34, MapDirection.Right),
            ]
        },
        [34] = new NextMapData
        {
            Id = 34,
            Name = "Đông Nam Guru",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(33, MapDirection.Left),
            ]
        },
        [35] = new NextMapData
        {
            Id = 35,
            Name = "Rừng cọ",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(17, MapDirection.Left),
                NextMapAdjacent.Waypoint(36, MapDirection.Right),
            ]
        },
        [36] = new NextMapData
        {
            Id = 36,
            Name = "Rừng đá",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(35, MapDirection.Left),
                NextMapAdjacent.Waypoint(37, MapDirection.Right),
            ]
        },
        [37] = new NextMapData
        {
            Id = 37,
            Name = "Thung lũng đen",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(36, MapDirection.Left),
                NextMapAdjacent.Waypoint(20, MapDirection.Center),
                NextMapAdjacent.Waypoint(38, MapDirection.Right),
            ]
        },
        [38] = new NextMapData
        {
            Id = 38,
            Name = "Bờ vực đen",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(37, MapDirection.Left),
            ]
        },
        [42] = new NextMapData
        {
            Id = 42,
            Name = "Vách núi Aru",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(0, MapDirection.Right),
            ]
        },
        [43] = new NextMapData
        {
            Id = 43,
            Name = "Vách núi Moori",
            PlanetId = 1,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(7, MapDirection.Right),
            ]
        },
        [44] = new NextMapData
        {
            Id = 44,
            Name = "Vách núi Kakarot",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(52, MapDirection.Left),
                NextMapAdjacent.Waypoint(14, MapDirection.Right),
            ]
        },
        [45] = new NextMapData
        {
            Id = 45,
            Name = "Thần điện",
            PlanetId = 0,
            IsAirMap = true,
            Adjacents =
            [
                NextMapAdjacent.AirDownward(46),
                NextMapAdjacent.Npc(48, 19, 3),
            ]
        },
        [46] = new NextMapData
        {
            Id = 46,
            Name = "Tháp Karin",
            PlanetId = 0,
            IsAirMap = true,
            Adjacents =
            [
                NextMapAdjacent.AirDownward(47),
                NextMapAdjacent.Waypoint(45, MapDirection.Center),
            ]
        },
        [47] = new NextMapData
        {
            Id = 47,
            Name = "Rừng Karin",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(1, MapDirection.Left),
                NextMapAdjacent.Waypoint(46, MapDirection.Center),
                NextMapAdjacent.Waypoint(111, MapDirection.Right),
            ]
        },
        [48] = new NextMapData
        {
            Id = 48,
            Name = "Hành tinh Kaio",
            PlanetId = 0,
            IsAirMap = true,
            Adjacents =
            [
                NextMapAdjacent.Npc(45, 20, 3, [0]),
                NextMapAdjacent.Npc(50, 20, 3, [1]),
            ]
        },
        [49] = new NextMapData
        {
            Id = 49,
            Name = "Phòng tập thời gian",
            PlanetId = 0,
            Adjacents = []
        },
        [50] = new NextMapData
        {
            Id = 50,
            Name = "Thánh địa Kaio",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Npc(48, 44, 0),
                NextMapAdjacent.Npc(154, 44, 1),
            ]
        },
        [52] = new NextMapData
        {
            Id = 52,
            Name = "Đại hội võ thuật",
            PlanetId = 2,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(44, MapDirection.Right),
            ]
        },
        [53] = new NextMapData
        {
            Id = 53,
            Name = "Tường thành 1",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(58, MapDirection.Right),
                NextMapAdjacent.Waypoint(27, MapDirection.Right),
            ]
        },
        [54] = new NextMapData
        {
            Id = 54,
            Name = "Tầng 3",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(56, MapDirection.Left),
                NextMapAdjacent.Waypoint(57, MapDirection.Right),
            ]
        },
        [55] = new NextMapData
        {
            Id = 55,
            Name = "Tầng 1",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(62, MapDirection.Left),
                NextMapAdjacent.Waypoint(56, MapDirection.Right),
            ]
        },
        [56] = new NextMapData
        {
            Id = 56,
            Name = "Tầng 2",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(55, MapDirection.Left),
                NextMapAdjacent.Waypoint(54, MapDirection.Right),
            ]
        },
        [57] = new NextMapData
        {
            Id = 57,
            Name = "Tầng 4",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(54, MapDirection.Left),
            ]
        },
        [58] = new NextMapData
        {
            Id = 58,
            Name = "Tường thành 2",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(53, MapDirection.Left),
                NextMapAdjacent.Waypoint(59, MapDirection.Right),
            ]
        },
        [59] = new NextMapData
        {
            Id = 59,
            Name = "Tường thành 3",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(58, MapDirection.Left),
                NextMapAdjacent.Waypoint(60, MapDirection.Right),
            ]
        },
        [60] = new NextMapData
        {
            Id = 60,
            Name = "Trại độc nhãn 1",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(59, MapDirection.Left),
                NextMapAdjacent.Waypoint(61, MapDirection.Right),
            ]
        },
        [61] = new NextMapData
        {
            Id = 61,
            Name = "Trại độc nhãn 2",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(60, MapDirection.Left),
                NextMapAdjacent.Waypoint(62, MapDirection.Right),
            ]
        },
        [62] = new NextMapData
        {
            Id = 62,
            Name = "Trại độc nhãn 3",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(61, MapDirection.Left),
                NextMapAdjacent.Waypoint(55, MapDirection.Right),
            ]
        },
        [63] = new NextMapData
        {
            Id = 63,
            Name = "Trại lính Fide",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(65, MapDirection.Left),
                NextMapAdjacent.Waypoint(66, MapDirection.Right),
            ]
        },
        [64] = new NextMapData
        {
            Id = 64,
            Name = "Núi dây leo",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(72, MapDirection.Left),
                NextMapAdjacent.Waypoint(65, MapDirection.Right),
            ]
        },
        [65] = new NextMapData
        {
            Id = 65,
            Name = "Núi cây quỷ",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(64, MapDirection.Left),
                NextMapAdjacent.Waypoint(63, MapDirection.Right),
            ]
        },
        [66] = new NextMapData
        {
            Id = 66,
            Name = "Trại quỷ già",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(63, MapDirection.Left),
                NextMapAdjacent.Waypoint(67, MapDirection.Right),
            ]
        },
        [67] = new NextMapData
        {
            Id = 67,
            Name = "Vực chết",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(66, MapDirection.Left),
                NextMapAdjacent.Waypoint(73, MapDirection.Right),
            ]
        },
        [68] = new NextMapData
        {
            Id = 68,
            Name = "Thung lũng Nappa",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(69, MapDirection.Right),
                NextMapAdjacent.Npc(19, 12, 0),
            ]
        },
        [69] = new NextMapData
        {
            Id = 69,
            Name = "Vực cấm",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(68, MapDirection.Left),
                NextMapAdjacent.Waypoint(70, MapDirection.Right),
            ]
        },
        [70] = new NextMapData
        {
            Id = 70,
            Name = "Núi Appule",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(69, MapDirection.Left),
                NextMapAdjacent.Waypoint(71, MapDirection.Right),
            ]
        },
        [71] = new NextMapData
        {
            Id = 71,
            Name = "Căn cứ Raspberry",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(70, MapDirection.Left),
                NextMapAdjacent.Waypoint(72, MapDirection.Right),
            ]
        },
        [72] = new NextMapData
        {
            Id = 72,
            Name = "Thung lũng Raspberry",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(71, MapDirection.Left),
                NextMapAdjacent.Waypoint(64, MapDirection.Right),
            ]
        },
        [73] = new NextMapData
        {
            Id = 73,
            Name = "Thung lũng chết",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(67, MapDirection.Left),
                NextMapAdjacent.Waypoint(74, MapDirection.Right),
            ]
        },
        [74] = new NextMapData
        {
            Id = 74,
            Name = "Đồi cây Fide",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(73, MapDirection.Left),
                NextMapAdjacent.Waypoint(75, MapDirection.Right),
            ]
        },
        [75] = new NextMapData
        {
            Id = 75,
            Name = "Khe núi tử thần",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(74, MapDirection.Left),
                NextMapAdjacent.Waypoint(76, MapDirection.Right),
            ]
        },
        [76] = new NextMapData
        {
            Id = 76,
            Name = "Núi đá",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(75, MapDirection.Left),
                NextMapAdjacent.Waypoint(77, MapDirection.Right),
            ]
        },
        [77] = new NextMapData
        {
            Id = 77,
            Name = "Rừng đá",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(76, MapDirection.Left),
                NextMapAdjacent.Waypoint(81, MapDirection.Right),
            ]
        },
        [79] = new NextMapData
        {
            Id = 79,
            Name = "Núi khỉ đỏ",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(83, MapDirection.Left),
                NextMapAdjacent.Waypoint(80, MapDirection.Right),
            ]
        },
        [80] = new NextMapData
        {
            Id = 80,
            Name = "Núi khỉ vàng",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(79, MapDirection.Left),
                NextMapAdjacent.Waypoint(105, MapDirection.Right),
                NextMapAdjacent.Npc(131, 60, 0),
            ]
        },
        [81] = new NextMapData
        {
            Id = 81,
            Name = "Hang quỷ chim",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(77, MapDirection.Left),
                NextMapAdjacent.Waypoint(82, MapDirection.Right),
            ]
        },
        [82] = new NextMapData
        {
            Id = 82,
            Name = "Núi khỉ đen",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(81, MapDirection.Left),
                NextMapAdjacent.Waypoint(83, MapDirection.Right),
            ]
        },
        [83] = new NextMapData
        {
            Id = 83,
            Name = "Hang khỉ đen",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(82, MapDirection.Left),
                NextMapAdjacent.Waypoint(79, MapDirection.Right),
            ]
        },
        [84] = new NextMapData
        {
            Id = 84,
            Name = "Siêu Thị",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(104),
                NextMapAdjacent.Npc(24, 10, 0),
                NextMapAdjacent.Npc(25, 10, 0),
                NextMapAdjacent.Npc(26, 10, 0),
                NextMapAdjacent.Npc(85, 10, 0),
                NextMapAdjacent.Npc(86, 10, 0),
                NextMapAdjacent.Npc(87, 10, 0),
                NextMapAdjacent.Npc(88, 10, 0),
                NextMapAdjacent.Npc(89, 10, 0),
                NextMapAdjacent.Npc(90, 10, 0),
                NextMapAdjacent.Npc(91, 10, 0),
            ]
        },
        [85] = new NextMapData
        {
            Id = 85,
            Name = "Hành tinh M-2",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Npc(24, 30, 0),
                NextMapAdjacent.Npc(25, 30, 0),
                NextMapAdjacent.Npc(26, 30, 0),
            ]
        },
        [86] = new NextMapData
        {
            Id = 86,
            Name = "Hành tinh Polaris",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Npc(24, 31, 0),
                NextMapAdjacent.Npc(25, 31, 0),
                NextMapAdjacent.Npc(26, 31, 0),
            ]
        },
        [87] = new NextMapData
        {
            Id = 87,
            Name = "Hành tinh Cretaceous",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Npc(24, 32, 0),
                NextMapAdjacent.Npc(25, 32, 0),
                NextMapAdjacent.Npc(26, 32, 0),
            ]
        },
        [88] = new NextMapData
        {
            Id = 88,
            Name = "Hành tinh Monmaasu",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Npc(24, 33, 0),
                NextMapAdjacent.Npc(25, 33, 0),
                NextMapAdjacent.Npc(26, 33, 0),
            ]
        },
        [89] = new NextMapData
        {
            Id = 89,
            Name = "Hành tinh Rudeeze",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Npc(24, 34, 0),
                NextMapAdjacent.Npc(25, 34, 0),
                NextMapAdjacent.Npc(26, 34, 0),
            ]
        },
        [90] = new NextMapData
        {
            Id = 90,
            Name = "Hành tinh Gelbo",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Npc(24, 35, 0),
                NextMapAdjacent.Npc(25, 35, 0),
                NextMapAdjacent.Npc(26, 35, 0),
            ]
        },
        [91] = new NextMapData
        {
            Id = 91,
            Name = "Hành tinh Tigere",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Npc(24, 36, 0),
                NextMapAdjacent.Npc(25, 36, 0),
                NextMapAdjacent.Npc(26, 36, 0),
            ]
        },
        [92] = new NextMapData
        {
            Id = 92,
            Name = "Thành phố phía đông",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(102, MapDirection.Left),
                NextMapAdjacent.Waypoint(93, MapDirection.Right),
            ]
        },
        [93] = new NextMapData
        {
            Id = 93,
            Name = "Thành phố phía nam",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(92, MapDirection.Left),
                NextMapAdjacent.Waypoint(94, MapDirection.Right),
            ]
        },
        [94] = new NextMapData
        {
            Id = 94,
            Name = "Đảo Balê",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(93, MapDirection.Left),
                NextMapAdjacent.Waypoint(96, MapDirection.Right),
            ]
        },
        [96] = new NextMapData
        {
            Id = 96,
            Name = "Cao nguyên",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(94, MapDirection.Left),
                NextMapAdjacent.Waypoint(97, MapDirection.Right),
            ]
        },
        [97] = new NextMapData
        {
            Id = 97,
            Name = "Thành phố phía bắc",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(96, MapDirection.Left),
                NextMapAdjacent.Waypoint(98, MapDirection.Right),
            ]
        },
        [98] = new NextMapData
        {
            Id = 98,
            Name = "Ngọn núi phía bắc",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(97, MapDirection.Left),
                NextMapAdjacent.Waypoint(99, MapDirection.Right),
            ]
        },
        [99] = new NextMapData
        {
            Id = 99,
            Name = "Thung lũng phía bắc",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(98, MapDirection.Left),
                NextMapAdjacent.Waypoint(100, MapDirection.Right),
            ]
        },
        [100] = new NextMapData
        {
            Id = 100,
            Name = "Thị trấn Ginder",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(99, MapDirection.Left),
                NextMapAdjacent.Waypoint(103, MapDirection.Right),
            ]
        },
        [102] = new NextMapData
        {
            Id = 102,
            Name = "Nhà Bunma",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(92, MapDirection.Right),
                NextMapAdjacent.Npc(24, 38, 1),
            ]
        },
        [103] = new NextMapData
        {
            Id = 103,
            Name = "Võ đài Xên bọ hung",
            PlanetId = 5,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(100, MapDirection.Left),
            ]
        },
        [104] = new NextMapData
        {
            Id = 104,
            Name = "Sân sau siêu thị",
            PlanetId = 6,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(84),
            ]
        },
        [105] = new NextMapData
        {
            Id = 105,
            Name = "Cánh đồng tuyết",
            PlanetId = 4,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(80, MapDirection.Left),
                NextMapAdjacent.Waypoint(108, MapDirection.Center),
                NextMapAdjacent.Waypoint(109, MapDirection.Right),
            ]
        },
        [106] = new NextMapData
        {
            Id = 106,
            Name = "Rừng tuyết",
            PlanetId = 4,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(109, MapDirection.Left),
                NextMapAdjacent.Waypoint(110, MapDirection.Center),
                NextMapAdjacent.Waypoint(107, MapDirection.Right),
            ]
        },
        [107] = new NextMapData
        {
            Id = 107,
            Name = "Núi tuyết",
            PlanetId = 4,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(108, MapDirection.Left),
                NextMapAdjacent.Waypoint(110, MapDirection.Center),
                NextMapAdjacent.Waypoint(106, MapDirection.Right),
            ]
        },
        [108] = new NextMapData
        {
            Id = 108,
            Name = "Dòng sông băng",
            PlanetId = 4,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(105, MapDirection.Left),
                NextMapAdjacent.Waypoint(109, MapDirection.Center),
                NextMapAdjacent.Waypoint(107, MapDirection.Right),
            ]
        },
        [109] = new NextMapData
        {
            Id = 109,
            Name = "Rừng băng",
            PlanetId = 4,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(105, MapDirection.Left),
                NextMapAdjacent.Waypoint(108, MapDirection.Center),
                NextMapAdjacent.Waypoint(106, MapDirection.Right),
                NextMapAdjacent.Npc(19, 12, 0),
            ]
        },
        [110] = new NextMapData
        {
            Id = 110,
            Name = "Hang băng",
            PlanetId = 4,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(107, MapDirection.Left),
                NextMapAdjacent.Waypoint(106, MapDirection.Right),
            ]
        },
        [111] = new NextMapData
        {
            Id = 111,
            Name = "Đông Nam Karin",
            PlanetId = 0,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(47, MapDirection.Left),
            ]
        },
        [126] = new NextMapData
        {
            Id = 126,
            Name = "Santa cũ",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Npc(19, 53, 0),
            ]
        },
        [131] = new NextMapData
        {
            Id = 131,
            Name = "Hành tinh Yardrat 1",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(132, MapDirection.Right),
                NextMapAdjacent.Npc(80, 60, 1),
            ]
        },
        [132] = new NextMapData
        {
            Id = 132,
            Name = "Hành tinh Yardrat 2",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(131, MapDirection.Left),
                NextMapAdjacent.Waypoint(133, MapDirection.Right),
            ]
        },
        [133] = new NextMapData
        {
            Id = 133,
            Name = "Hành tinh Yardrat 3",
            PlanetId = 3,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(132, MapDirection.Left),
            ]
        },
        [139] = new NextMapData
        {
            Id = 139,
            Name = "Hành tinh Potaufeu",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(140, MapDirection.Right),
                NextMapAdjacent.Npc(24, 63, 0),
                NextMapAdjacent.Npc(25, 63, 1),
                NextMapAdjacent.Npc(26, 63, 2),
            ]
        },
        [140] = new NextMapData
        {
            Id = 140,
            Name = "Hang động Potaufeu",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(139, MapDirection.Left),
            ]
        },
        [147] = new NextMapData
        {
            Id = 147,
            Name = "Sa mạc",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(149, MapDirection.Left),
                NextMapAdjacent.Waypoint(152, MapDirection.Right),
            ]
        },
        [148] = new NextMapData
        {
            Id = 148,
            Name = "Lâu đài Lychee",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(151, MapDirection.Left),
            ]
        },
        [149] = new NextMapData
        {
            Id = 149,
            Name = "Thành phố Santa",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(147, MapDirection.Right),
            ]
        },
        [151] = new NextMapData
        {
            Id = 151,
            Name = "Hành tinh bóng tối",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(152, MapDirection.Left),
                NextMapAdjacent.Waypoint(148, MapDirection.Right),
            ]
        },
        [152] = new NextMapData
        {
            Id = 152,
            Name = "Vùng đất băng giá",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(147, MapDirection.Left),
                NextMapAdjacent.Waypoint(151, MapDirection.Right),
            ]
        },
        [153] = new NextMapData
        {
            Id = 153,
            Name = "Lãnh địa bang hội",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Npc(5, 10, 0),
                NextMapAdjacent.Npc(156, 47, 0),
            ]
        },
        [154] = new NextMapData
        {
            Id = 154,
            Name = "Hành tinh Bill",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Npc(50, 55, 0),
                NextMapAdjacent.Npc(155, 44, 1),
            ]
        },
        [155] = new NextMapData
        {
            Id = 155,
            Name = "Hành tinh ngục tù",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Npc(154, 44, 1),
                NextMapAdjacent.Waypoint(166),
            ]
        },
        [156] = new NextMapData
        {
            Id = 156,
            Name = "Tây thánh địa",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(157, MapDirection.Right),
            ]
        },
        [157] = new NextMapData
        {
            Id = 157,
            Name = "Đông thánh Địa",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(156, MapDirection.Left),
                NextMapAdjacent.Waypoint(158, MapDirection.Right),
            ]
        },
        [158] = new NextMapData
        {
            Id = 158,
            Name = "Bắc thánh địa",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(157, MapDirection.Left),
                NextMapAdjacent.Waypoint(159, MapDirection.Right),
            ]
        },
        [159] = new NextMapData
        {
            Id = 159,
            Name = "Nam thánh Địa",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(158, MapDirection.Left),
            ]
        },
        [160] = new NextMapData
        {
            Id = 160,
            Name = "Khu hang động",
            PlanetId = 7,
            Adjacents = []
        },
        [166] = new NextMapData
        {
            Id = 166,
            Name = "Hành tinh ngục tù",
            PlanetId = 7,
            Adjacents =
            [
                NextMapAdjacent.Waypoint(155),
            ]
        },
    };

    public static bool TryGetMap(int mapId, out NextMapData? map)
    {
        lock (Lock)
        {
            return Maps.TryGetValue(mapId, out map);
        }
    }

    public static string GetMapName(int mapId)
    {
        lock (Lock)
        {
            return Maps.TryGetValue(mapId, out var m) ? m.Name : $"Map #{mapId}";
        }
    }

    public static int GetPlanetId(int mapId)
    {
        lock (Lock)
        {
            return Maps.TryGetValue(mapId, out var m) ? m.PlanetId : 7;
        }
    }

    public static int GetExpectedWaypointCount(int mapId, int gender = 0)
    {
        if (TryGetMap(mapId, out var mapData) && mapData != null)
        {
            int count = mapData.Adjacents.Count(a => a.TransitionType == NextMapTransitionType.Waypoint);
            if (mapId == 0 && gender != 0 && count > 0) count--;
            if (mapId == 7 && gender != 1 && count > 0) count--;
            if (mapId == 14 && gender != 2 && count > 0) count--;
            return Math.Max(0, count);
        }
        return 0;
    }
}
