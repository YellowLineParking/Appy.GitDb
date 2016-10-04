﻿using System.Linq;
using FluentAssertions;
using LibGit2Sharp;
using Newtonsoft.Json;
using Ylp.GitDb.Core.Model;

namespace Ylp.GitDb.Tests.Local.Utils
{
    public static class Behaviors
    {
        public static void HasTheCorrectMetaData(this Commit commit, string expectedMessage, Author expectedAuthor)
        {
            commit.Message.Should().Be(expectedMessage);
            commit.Author.Name.Should().Be(expectedAuthor.Name);
            commit.Author.Email.Should().Be(expectedAuthor.Email);
        }

        public static void HasTheCorrectData(this Commit commit, string expectedKey, string expectedValue)
        {
            var entry = commit.Tree.First();
            entry.Path.Should().Be(expectedKey);
            ((Blob) entry.Target).GetContentText().Should().Be(expectedValue);
        }

        public static void HasTheCorrectData<T>(this Commit commit, string expectedKey, T expectedValue) =>
            commit.HasTheCorrectData(expectedKey, JsonConvert.SerializeObject(expectedValue));
    }
}
