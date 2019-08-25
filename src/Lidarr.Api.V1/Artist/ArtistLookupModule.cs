using Nancy.Configuration;
using System.Collections.Generic;
using System.Linq;
using Nancy;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource;
using Lidarr.Http;
using Lidarr.Http.Extensions;

namespace Lidarr.Api.V1.Artist
{
    public class ArtistLookupModule : LidarrRestModule<ArtistResource>
    {
        private readonly ISearchForNewArtist _searchProxy;

        public ArtistLookupModule(INancyEnvironment environment, ISearchForNewArtist searchProxy)
            : base(environment, "/artist/lookup")
        {
            _searchProxy = searchProxy;
            Get("/",  x => Search());
        }

        private Response Search()
        {
            var searchResults = _searchProxy.SearchForNewArtist((string)Request.Query.term);
            return MapToResource(searchResults).ToList().AsResponse(_environment);
        }

        private static IEnumerable<ArtistResource> MapToResource(IEnumerable<NzbDrone.Core.Music.Artist> artist)
        {
            foreach (var currentArtist in artist)
            {
                var resource = currentArtist.ToResource();
                var poster = currentArtist.Metadata.Value.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Poster);
                if (poster != null)
                {
                    resource.RemotePoster = poster.Url;
                }

                yield return resource;
            }
        }
    }
}
