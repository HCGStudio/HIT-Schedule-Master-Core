﻿using System;
using System.Collections.Generic;
using System.IO;
using ExcelDataReader;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace HCGStudio.HITScheduleMasterCore
{
    public enum Semester
    {
        Spring = 0,
        Autumn = 2,
        Summer = 1
    }

    public class Schedule
    {
        public Schedule(Stream xlsStream)
        {
            var cal = new Calendar();

            var reader = ExcelReaderFactory.CreateReader(xlsStream);
            var table = reader.AsDataSet().Tables[0];
            if (!(table.Rows[0][0] is string tableHead)) throw new ArgumentException("错误的文件格式。");
            Year = int.Parse(tableHead[..4]);
            Semester = tableHead[4] switch
            {
                '春' => Semester.Spring,
                '夏' => Semester.Summer,
                _ => Semester.Autumn
            };
            for (var i = 0; i < 7; i++) //列
                for (var j = 0; j < 5; j++) //行
                {
                    var current = table.Rows[j + 2][i + 2] as string;
                    if (string.IsNullOrWhiteSpace(current))
                        continue;
                    var next = table.Rows[j + 3][i + 2] as string;
                    var currentCourses = current.Replace("周\n", "周").Split('\n');
                    if (currentCourses.Length%2!=0)
                    {
                        throw new Exception("课表格式错误。");
                    }
                    for (var c = 0; c < currentCourses.Length; c += 2)
                    {
                        Entries.Add(new ScheduleEntry((DayOfWeek)((i + 1) % 7),
                            (CourseTime)(j + 1),
                            currentCourses[c],  currentCourses[c + 1],
                            current == next));
                    }

                    if (current == next) j++;
                }
        }

        public Schedule(int year, Semester semester)
        {
            Year = year;
            Semester = semester;
        }

        /// <summary>
        ///     只用于JSON导入
        /// </summary>
        public Schedule()
        {
        }

        private static DateTime[] SemesterStarts => new[]
        {
            new DateTime(2020, 02, 24),
            new DateTime(2020, 06, 29),
            new DateTime(2020, 08, 31),
            new DateTime(2021, 02, 22),
            new DateTime(2021, 06, 28)
        };

        public List<ScheduleEntry> Entries { get; } = new List<ScheduleEntry>();
        public int Year { get; }
        public DateTime SemesterStart => SemesterStarts[Year - 2020 + (int)Semester];
        public Semester Semester { get; set; }

        public Calendar GetCalendar()
        {
            var calendar = new Calendar();

            foreach (var entry in Entries)
            {
                var i = 0;
                var dayOfWeek = entry.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)entry.DayOfWeek - 1;
                for (var w = entry.Week >> 1; w != 0; w >>= 1, i++)
                {
                    if ((w & 1) != 1) continue;
                    var cEvent = new CalendarEvent
                    {
                        Location = entry.Location,
                        Start = new CalDateTime(
                            SemesterStart.AddDays(i * 7 + dayOfWeek) + entry.StartTime),
                        Duration = entry.Length,
                        Summary = $"{entry.CourseName} by {entry.Teacher} at {entry.Location}"
                    };
                    cEvent.Alarms.Add(new Alarm
                    {
                        Summary = $"您在{entry.Location}有一节{entry.CourseName}课程即将开始。",
                        Action = AlarmAction.Display,
                        Trigger = new Trigger(TimeSpan.FromMinutes(-25))
                    });
                    calendar.Events.Add(cEvent);
                }
            }

            //var sem = Semester switch
            //{
            //    Semester.Autumn => "秋",
            //    Semester.Summer => "夏",
            //    _ => "春"
            //};
            //calendar.Name = $"{Year}{sem}课程表";
            return calendar;
        }
    }
}