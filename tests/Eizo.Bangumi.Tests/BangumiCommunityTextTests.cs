using Eizo.Models;

namespace Eizo.Bangumi.Tests;

public sealed class BangumiCommunityTextTests
{
    [Fact]
    public void ParseContentBlocks_RecognizesBangumiPhotoTag()
    {
        var blocks =
            BangumiCommunityText.ParseContentBlocks(
                "before\n[photo]cf/93/416773_zORwS.jpg[/photo]\nafter");

        Assert.Equal(3, blocks.Count);
        Assert.Equal("before", blocks[0].Text);
        Assert.True(blocks[1].IsImage);
        Assert.Equal(
            "https://lain.bgm.tv/pic/photo/l/cf/93/416773_zORwS.jpg",
            blocks[1].ImageUrl);
        Assert.Equal("after", blocks[2].Text);
    }

    [Fact]
    public void ParseContentBlocks_RecognizesPhotoAttributeAndHtmlImage()
    {
        var blocks =
            BangumiCommunityText.ParseContentBlocks(
                "[photo=cf/93/416773_3vvsw.jpg]\n" +
                "<img src=\"cf/93/416773_4GigG.jpg\">");

        Assert.Equal(2, blocks.Count);
        Assert.All(
            blocks,
            static block =>
                Assert.True(block.IsImage));
        Assert.Equal(
            "https://lain.bgm.tv/pic/photo/l/cf/93/416773_3vvsw.jpg",
            blocks[0].ImageUrl);
        Assert.Equal(
            "https://lain.bgm.tv/pic/photo/l/cf/93/416773_4GigG.jpg",
            blocks[1].ImageUrl);
    }

    [Fact]
    public void ParseContentBlocks_RecognizesBareBangumiPhotoPath()
    {
        var blocks =
            BangumiCommunityText.ParseContentBlocks(
                "cf/93/416773_V8c9J.jpg");

        var image = Assert.Single(blocks);
        Assert.True(image.IsImage);
        Assert.Equal(
            "https://lain.bgm.tv/pic/photo/l/cf/93/416773_V8c9J.jpg",
            image.ImageUrl);
    }
}
