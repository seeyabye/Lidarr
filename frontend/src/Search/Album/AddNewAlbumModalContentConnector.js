import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { setAddDefault, addAlbum } from 'Store/Actions/searchActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import AddNewAlbumModalContent from './AddNewAlbumModalContent';

function createMapStateToProps() {
  return createSelector(
    (state, { isExistingArtist }) => isExistingArtist,
    (state) => state.search,
    (state) => state.settings.languageProfiles,
    (state) => state.settings.metadataProfiles,
    createDimensionsSelector(),
    (isExistingArtist, searchState, languageProfiles, metadataProfiles, dimensions) => {
      const {
        isAdding,
        addError,
        defaults
      } = searchState;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(defaults, {}, addError);

      return {
        isAdding,
        addError,
        showLanguageProfile: !isExistingArtist && languageProfiles.items.length > 1,
        showMetadataProfile: !isExistingArtist && metadataProfiles.items.length > 1,
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  setAddDefault,
  addAlbum
};

class AddNewAlbumModalContentConnector extends Component {

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setAddDefault({ [name]: value });
  }

  onAddAlbumPress = (searchForNewAlbum) => {
    const {
      foreignAlbumId,
      rootFolderPath,
      monitor,
      qualityProfileId,
      languageProfileId,
      metadataProfileId,
      albumFolder,
      tags
    } = this.props;

    this.props.addAlbum({
      foreignAlbumId,
      rootFolderPath: rootFolderPath.value,
      monitor: monitor.value,
      qualityProfileId: qualityProfileId.value,
      languageProfileId: languageProfileId.value,
      metadataProfileId: metadataProfileId.value,
      albumFolder: albumFolder.value,
      tags: tags.value,
      searchForNewAlbum
    });
  }

  //
  // Render

  render() {
    return (
      <AddNewAlbumModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onAddAlbumPress={this.onAddAlbumPress}
      />
    );
  }
}

AddNewAlbumModalContentConnector.propTypes = {
  isExistingArtist: PropTypes.bool.isRequired,
  foreignAlbumId: PropTypes.string.isRequired,
  rootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  languageProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  albumFolder: PropTypes.object.isRequired,
  tags: PropTypes.object.isRequired,
  onModalClose: PropTypes.func.isRequired,
  setAddDefault: PropTypes.func.isRequired,
  addAlbum: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewAlbumModalContentConnector);
